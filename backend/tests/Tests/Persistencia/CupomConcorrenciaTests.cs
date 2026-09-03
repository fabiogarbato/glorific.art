using Glorific.Domain.Interfaces.Repositories;
using Glorific.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// Cupom "primeiros N" sob concorrencia.
///
/// O caso que motiva o teste e comercial, nao tecnico: o cupom limitado a tres usos e anunciado
/// em story com contagem regressiva, e dez pessoas apertam finalizar no mesmo segundo. Ler
/// usos_atuais e depois incrementar deixa todas passarem pela leitura antes de qualquer escrita,
/// e o lojista honra dez descontos que nunca autorizou. O UPDATE condicional resolve no banco,
/// em uma instrucao, e e isso que este arquivo prova contra Postgres REAL.
/// </summary>
[Collection(BancoCollection.Nome)]
public sealed class CupomConcorrenciaTests : IAsyncLifetime
{
    private const int Concorrentes = 10;
    private const int UsoMaximoTotal = 3;

    private readonly BancoFixture _banco;

    public CupomConcorrenciaTests(BancoFixture banco) => _banco = banco;

    public Task InitializeAsync() => _banco.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TentarConsumirUsoAsync_DezTentativasConcorrentesComUsoMaximoTotalTres_ConsomeExatamenteTres()
    {
        int idCupom;

        await using (var contexto = _banco.CriarContexto())
        {
            var cupom = await DadosPersistencia.CriarCupomAsync(contexto, "PRIMEIROS3", UsoMaximoTotal);
            idCupom = cupom.Id;
        }

        var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tarefas = new Task<bool>[Concorrentes];

        for (var i = 0; i < Concorrentes; i++)
        {
            tarefas[i] = Task.Run(async () =>
            {
                await largada.Task;

                using var escopo = _banco.CriarEscopo();
                var repositorio = escopo.ServiceProvider.GetRequiredService<ICupomRepository>();

                return await repositorio.TentarConsumirUsoAsync(idCupom);
            });
        }

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        Assert.Equal(UsoMaximoTotal, resultados.Count(sucesso => sucesso));
        Assert.Equal(Concorrentes - UsoMaximoTotal, resultados.Count(sucesso => !sucesso));

        await using var verificacao = _banco.CriarContexto();
        var usos = await verificacao.Cupons
            .AsNoTracking()
            .Where(c => c.Id == idCupom)
            .Select(c => c.UsosAtuais)
            .SingleAsync();

        // O contador nao pode passar do teto nem ficar abaixo dos sucessos reportados.
        Assert.Equal(UsoMaximoTotal, usos);
    }

    [Fact]
    public async Task TentarConsumirUsoAsync_CupomJaEsgotado_DevolveFalseSemIncrementar()
    {
        int idCupom;

        await using (var contexto = _banco.CriarContexto())
        {
            var cupom = await DadosPersistencia.CriarCupomAsync(contexto, "ESGOTADO", usoMaximoTotal: 1);
            idCupom = cupom.Id;
        }

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<ICupomRepository>();

        Assert.True(await repositorio.TentarConsumirUsoAsync(idCupom));
        Assert.False(await repositorio.TentarConsumirUsoAsync(idCupom));

        await using var verificacao = _banco.CriarContexto();
        var usos = await verificacao.Cupons.AsNoTracking()
            .Where(c => c.Id == idCupom).Select(c => c.UsosAtuais).SingleAsync();

        Assert.Equal(1, usos);
    }

    [Fact]
    public async Task TentarConsumirUsoAsync_CupomInativo_DevolveFalse()
    {
        int idCupom;

        await using (var contexto = _banco.CriarContexto())
        {
            var cupom = await DadosPersistencia.CriarCupomAsync(
                contexto, "DESLIGADO", usoMaximoTotal: 100, ativo: false);
            idCupom = cupom.Id;
        }

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<ICupomRepository>();

        Assert.False(await repositorio.TentarConsumirUsoAsync(idCupom));
    }

    [Fact]
    public async Task TentarConsumirUsoAsync_UsoMaximoTotalNulo_NaoLimitaAsTentativasConcorrentes()
    {
        int idCupom;

        await using (var contexto = _banco.CriarContexto())
        {
            // Null significa ilimitado — o WHERE precisa deixar todas passarem.
            var cupom = await DadosPersistencia.CriarCupomAsync(contexto, "ILIMITADO", usoMaximoTotal: null);
            idCupom = cupom.Id;
        }

        var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tarefas = new Task<bool>[Concorrentes];

        for (var i = 0; i < Concorrentes; i++)
        {
            tarefas[i] = Task.Run(async () =>
            {
                await largada.Task;

                using var escopo = _banco.CriarEscopo();
                var repositorio = escopo.ServiceProvider.GetRequiredService<ICupomRepository>();

                return await repositorio.TentarConsumirUsoAsync(idCupom);
            });
        }

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        Assert.All(resultados, sucesso => Assert.True(sucesso));

        await using var verificacao = _banco.CriarContexto();
        var usos = await verificacao.Cupons.AsNoTracking()
            .Where(c => c.Id == idCupom).Select(c => c.UsosAtuais).SingleAsync();

        // Dez incrementos concorrentes, nenhum perdido: e o teste de lost update do contador.
        Assert.Equal(Concorrentes, usos);
    }

    [Fact]
    public async Task DevolverUsoAsync_ContadorZerado_NaoDeixaUsosAtuaisNegativo()
    {
        int idCupom;

        await using (var contexto = _banco.CriarContexto())
        {
            var cupom = await DadosPersistencia.CriarCupomAsync(contexto, "COMPENSA", usoMaximoTotal: 5);
            idCupom = cupom.Id;
        }

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<ICupomRepository>();

        // Compensacao executada duas vezes para um consumo so — acontece quando o checkout
        // falha e a retentativa tambem compensa.
        Assert.True(await repositorio.TentarConsumirUsoAsync(idCupom));
        await repositorio.DevolverUsoAsync(idCupom);
        await repositorio.DevolverUsoAsync(idCupom);

        await using var verificacao = _banco.CriarContexto();
        var usos = await verificacao.Cupons.AsNoTracking()
            .Where(c => c.Id == idCupom).Select(c => c.UsosAtuais).SingleAsync();

        Assert.Equal(0, usos);
    }
}
