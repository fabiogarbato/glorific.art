using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Glorific.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// Idempotencia de webhook decidida pelo BANCO.
///
/// O gateway reentrega. Sempre. O "select antes de inserir" nao resolve, porque duas reentregas
/// simultaneas passam as duas pelo select e a segunda estoura 23505 crua na cara do gateway —
/// que reage reentregando de novo. Aqui o indice unico ux_pagamentos_eventos_provider_event_id
/// e o arbitro, e a violacao vira "false", nao excecao.
///
/// O detalhe caro e o ULTIMO teste: como TentarRegistrarEventoAsync e o unico ponto da camada
/// que flusha, ele roda dentro da transacao do caso de uso. Se a violacao abortasse a transacao,
/// todo o checkout/processamento em curso morreria junto. O EF cria savepoint antes do
/// SaveChanges e volta a ele no erro; este arquivo prova que a transacao do chamador continua
/// utilizavel e commitavel depois da duplicata.
/// </summary>
[Collection(BancoCollection.Nome)]
public sealed class IdempotenciaPagamentoTests : IAsyncLifetime
{
    private readonly BancoFixture _banco;

    public IdempotenciaPagamentoTests(BancoFixture banco) => _banco = banco;

    public Task InitializeAsync() => _banco.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TentarRegistrarEventoAsync_MesmoProviderEventIdDuasVezes_SegundaDevolveFalseSemLancar()
    {
        const string eventoDoGateway = "evt_reentrega_001";

        using var escopoPrimeira = _banco.CriarEscopo();
        var primeira = escopoPrimeira.ServiceProvider.GetRequiredService<IPagamentoRepository>();

        Assert.True(await primeira.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway)));

        // Escopo novo = contexto novo, exatamente como a segunda entrega chegaria na API.
        using var escopoSegunda = _banco.CriarEscopo();
        var segunda = escopoSegunda.ServiceProvider.GetRequiredService<IPagamentoRepository>();

        var gravou = await segunda.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway));

        Assert.False(gravou);

        await using var verificacao = _banco.CriarContexto();
        var total = await verificacao.PagamentosEventos
            .AsNoTracking()
            .CountAsync(e => e.ProviderEventId == eventoDoGateway);

        Assert.Equal(1, total);
    }

    [Fact]
    public async Task TentarRegistrarEventoAsync_EventoDuplicado_DesanexaAEntidadeRecusada()
    {
        const string eventoDoGateway = "evt_reentrega_002";

        using var escopo = _banco.CriarEscopo();
        var contexto = escopo.ServiceProvider.GetRequiredService<GlorificContext>();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IPagamentoRepository>();

        Assert.True(await repositorio.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway)));

        var duplicado = DadosPersistencia.NovoEvento(eventoDoGateway);
        Assert.False(await repositorio.TentarRegistrarEventoAsync(duplicado));

        // Se ficasse pendurado como Added, o proximo SaveChanges do caso de uso tentaria inserir
        // de novo e a excecao estouraria FORA do try do repositorio.
        Assert.Equal(EntityState.Detached, contexto.Entry(duplicado).State);

        // Prova pratica: o SaveChanges seguinte nao pode falhar nem gravar o duplicado.
        var linhas = await contexto.SaveChangesAsync();

        Assert.Equal(0, linhas);

        await using var verificacao = _banco.CriarContexto();
        Assert.Equal(
            1,
            await verificacao.PagamentosEventos.AsNoTracking()
                .CountAsync(e => e.ProviderEventId == eventoDoGateway));
    }

    [Fact]
    public async Task TentarRegistrarEventoAsync_DuplicadoDentroDeTransacao_MantemATransacaoDoChamadorUtilizavel()
    {
        const string eventoDoGateway = "evt_reentrega_003";

        using var escopo = _banco.CriarEscopo();
        var contexto = escopo.ServiceProvider.GetRequiredService<GlorificContext>();
        var unidade = escopo.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IPagamentoRepository>();

        await using var transacao = await unidade.BeginTransactionAsync();

        Assert.True(await repositorio.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway)));

        // A duplicata acontece DENTRO da transacao aberta pelo caso de uso.
        Assert.False(await repositorio.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway)));

        // Se o 23505 tivesse abortado a transacao, este trecho morreria com
        // "current transaction is aborted, commands ignored until end of transaction block".
        var cupom = await DadosPersistencia.CriarCupomAsync(contexto, "POSDUPLICATA", usoMaximoTotal: 1);

        await transacao.CommitAsync();

        await using var verificacao = _banco.CriarContexto();

        Assert.Equal(
            1,
            await verificacao.PagamentosEventos.AsNoTracking()
                .CountAsync(e => e.ProviderEventId == eventoDoGateway));

        // O trabalho do chamador foi commitado normalmente junto com o evento aceito.
        Assert.True(await verificacao.Cupons.AsNoTracking().AnyAsync(c => c.Id == cupom.Id));
    }

    [Fact]
    public async Task TentarRegistrarEventoAsync_DuasReentregasConcorrentes_GravamUmaLinhaSo()
    {
        const string eventoDoGateway = "evt_reentrega_004";
        const int Concorrentes = 8;

        var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tarefas = new Task<bool>[Concorrentes];

        for (var i = 0; i < Concorrentes; i++)
        {
            tarefas[i] = Task.Run(async () =>
            {
                await largada.Task;

                using var escopo = _banco.CriarEscopo();
                var repositorio = escopo.ServiceProvider.GetRequiredService<IPagamentoRepository>();

                return await repositorio.TentarRegistrarEventoAsync(
                    DadosPersistencia.NovoEvento(eventoDoGateway));
            });
        }

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        // Exatamente uma entrega "ganha"; as outras sete recebem false, nao excecao.
        Assert.Equal(1, resultados.Count(gravou => gravou));

        await using var verificacao = _banco.CriarContexto();
        Assert.Equal(
            1,
            await verificacao.PagamentosEventos.AsNoTracking()
                .CountAsync(e => e.ProviderEventId == eventoDoGateway));
    }

    [Fact]
    public async Task Payload_GravadoNaColunaJsonb_VoltaComoJsonValidoDoBanco()
    {
        const string eventoDoGateway = "evt_payload_001";

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IPagamentoRepository>();

        Assert.True(await repositorio.TentarRegistrarEventoAsync(
            DadosPersistencia.NovoEvento(eventoDoGateway, "charge.refunded")));

        // jsonb de verdade: o proprio Postgres consegue navegar o documento. Em SQLite a coluna
        // seria texto e esta consulta nem existiria — e o motivo de a suite exigir Postgres.
        var tipos = await _banco.ConsultarColunaAsync(
            $"SELECT payload ->> 'type' FROM public.pagamentos_eventos WHERE provider_event_id = '{eventoDoGateway}'");

        var tipo = Assert.Single(tipos);
        Assert.Equal("charge.refunded", tipo);
    }
}
