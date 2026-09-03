using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// O coracao da suite: prova, contra Postgres REAL, que a loja nao vende a mesma peca duas vezes.
///
/// Por que isto nao pode ser teste de unidade com repositorio falso: o que impede o oversell nao
/// e uma linha de C#, e o WHERE do UPDATE sendo avaliado pelo banco dentro de UMA instrucao
/// atomica. Um dublê de repositorio em memoria devolveria o resultado que o autor do dublê
/// espera; so o Postgres, com MVCC e lock de linha de verdade, responde "quantas das 20 tarefas
/// realmente conseguiram".
///
/// Todas as tarefas concorrentes abrem o PROPRIO escopo de DI (logo, o proprio DbContext e a
/// propria conexao). Compartilhar contexto entre tarefas produziria falha intermitente do EF que
/// nada tem a ver com a regra sob teste.
/// </summary>
[Collection(BancoCollection.Nome)]
public sealed class EstoqueConcorrenciaTests : IAsyncLifetime
{
    private const int Concorrentes = 20;
    private const int EstoqueInicial = 5;

    private readonly BancoFixture _banco;

    public EstoqueConcorrenciaTests(BancoFixture banco) => _banco = banco;

    public Task InitializeAsync() => _banco.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TentarReservarAsync_VinteReservasConcorrentesDeUmaUnidadeSobreEstoqueCinco_TemExatamenteCincoSucessos()
    {
        int idVariacao;

        await using (var contexto = _banco.CriarContexto())
        {
            var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, EstoqueInicial);
            idVariacao = catalogo.IdVariacao;
        }

        // Largada unica: sem ela as tarefas se enfileiram pelo custo de abrir escopo e conexao,
        // e o teste passaria mesmo com um repositorio de ler-alterar-salvar.
        var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tarefas = new Task<bool>[Concorrentes];

        for (var i = 0; i < Concorrentes; i++)
        {
            tarefas[i] = Task.Run(async () =>
            {
                await largada.Task;

                using var escopo = _banco.CriarEscopo();
                var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

                return await repositorio.TentarReservarAsync(idVariacao, 1);
            });
        }

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        Assert.Equal(EstoqueInicial, resultados.Count(sucesso => sucesso));
        Assert.Equal(Concorrentes - EstoqueInicial, resultados.Count(sucesso => !sucesso));

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, idVariacao);

        // Reserva e SOFT: o fisico nao pode ter sido tocado por reserva nenhuma.
        Assert.Equal(EstoqueInicial, estoque.Quantidade);
        Assert.Equal(EstoqueInicial, estoque.QuantidadeReservada);
        Assert.Equal(0, estoque.Disponivel);
    }

    [Fact]
    public async Task TentarReservarAsync_QuantidadeMaiorQueODisponivel_DevolveFalseSemAlterarNada()
    {
        await using var contexto = _banco.CriarContexto();

        // Disponivel = 4 - 3 = 1. Pedir 2 tem que ser recusado.
        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 4, reservada: 3);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

        var reservou = await repositorio.TentarReservarAsync(catalogo.IdVariacao, 2);

        Assert.False(reservou);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        Assert.Equal(4, estoque.Quantidade);
        Assert.Equal(3, estoque.QuantidadeReservada);
    }

    [Fact]
    public async Task LiberarReservaAsync_ReservaExistente_DevolveAoDisponivelSemMexerNoFisico()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 10, reservada: 4);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

        var liberou = await repositorio.LiberarReservaAsync(catalogo.IdVariacao, 3);

        Assert.True(liberou);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        // Pagamento expirado devolve a reserva; a peca nunca saiu da prateleira.
        Assert.Equal(10, estoque.Quantidade);
        Assert.Equal(1, estoque.QuantidadeReservada);
        Assert.Equal(9, estoque.Disponivel);
    }

    [Fact]
    public async Task LiberarReservaAsync_QuantidadeMaiorQueOReservado_DevolveFalseSemDeixarReservaNegativa()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 10, reservada: 2);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

        // Cenario real: webhook de expiracao reentregue duas vezes.
        var liberou = await repositorio.LiberarReservaAsync(catalogo.IdVariacao, 5);

        Assert.False(liberou);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        Assert.Equal(10, estoque.Quantidade);
        Assert.Equal(2, estoque.QuantidadeReservada);
    }

    [Fact]
    public async Task TentarEfetivarVendaAsync_ReservaSuficiente_BaixaFisicoEReservaNaMesmaOperacao()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 8, reservada: 3);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

        var efetivou = await repositorio.TentarEfetivarVendaAsync(catalogo.IdVariacao, 2);

        Assert.True(efetivou);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        // Os dois caem juntos: separar em dois updates abriria a fresta em que a peca ja vendida
        // volta a aparecer como disponivel na vitrine.
        Assert.Equal(6, estoque.Quantidade);
        Assert.Equal(1, estoque.QuantidadeReservada);
        Assert.Equal(5, estoque.Disponivel);
    }

    [Fact]
    public async Task TentarEfetivarVendaAsync_QuantidadeMaiorQueOReservado_DevolveFalseSemAlterarNada()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 8, reservada: 2);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();

        var efetivou = await repositorio.TentarEfetivarVendaAsync(catalogo.IdVariacao, 3);

        Assert.False(efetivou);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        Assert.Equal(8, estoque.Quantidade);
        Assert.Equal(2, estoque.QuantidadeReservada);
    }

    [Fact]
    public async Task TentarReservarAsync_DepoisDeExecuteUpdate_NaoDeixaSaldoVelhoNoChangeTracker()
    {
        int idVariacao;

        await using (var preparacao = _banco.CriarContexto())
        {
            var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(preparacao, quantidade: 5);
            idVariacao = catalogo.IdVariacao;
        }

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEstoqueRepository>();
        var unidade = escopo.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Rastreia a linha ANTES do UPDATE atomico: e a armadilha documentada no repositorio.
        // Sem o Desanexar, este SaveChanges reescreveria reservada = 0 por cima da reserva.
        var rastreada = await repositorio.QueryTracked()
            .FirstAsync(e => e.IdVariacao == idVariacao);

        Assert.Equal(0, rastreada.QuantidadeReservada);

        Assert.True(await repositorio.TentarReservarAsync(idVariacao, 2));

        await unidade.SaveChangesAsync();

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, idVariacao);

        Assert.Equal(5, estoque.Quantidade);
        Assert.Equal(2, estoque.QuantidadeReservada);
    }

    [Fact]
    public async Task UpdateDireto_QueDeixariaReservadaMaiorQueQuantidade_ERejeitadoPeloCheckDoBanco()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 5, reservada: 1);

        // SQL cru de proposito: nenhum repositorio emitiria isto. O ponto e provar que a rede de
        // seguranca esta no BANCO, e nao no WHERE do C# — um caminho de codigo novo que esqueca
        // a condicao ainda assim nao consegue gravar estoque incoerente.
        var excecao = await Assert.ThrowsAsync<PostgresException>(async () => await _banco.ExecutarAsync(
            $"UPDATE public.estoques_variacoes SET quantidade_reservada = 99 WHERE id_variacao = {catalogo.IdVariacao}"));

        // 23514 = check_violation.
        Assert.Equal("23514", excecao.SqlState);
        Assert.Equal("ck_estoques_variacoes_quantidades", excecao.ConstraintName);

        await using var verificacao = _banco.CriarContexto();
        var estoque = await DadosPersistencia.LerEstoqueAsync(verificacao, catalogo.IdVariacao);

        Assert.Equal(5, estoque.Quantidade);
        Assert.Equal(1, estoque.QuantidadeReservada);
    }

    [Fact]
    public async Task UpdateDireto_QueDeixariaQuantidadeNegativa_ERejeitadoPeloCheckDoBanco()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 3);

        var excecao = await Assert.ThrowsAsync<PostgresException>(async () => await _banco.ExecutarAsync(
            $"UPDATE public.estoques_variacoes SET quantidade = -1 WHERE id_variacao = {catalogo.IdVariacao}"));

        Assert.Equal("23514", excecao.SqlState);
        Assert.Equal("ck_estoques_variacoes_quantidades", excecao.ConstraintName);
    }
}
