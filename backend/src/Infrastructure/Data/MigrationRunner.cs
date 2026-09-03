using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// Aplica migrations no boot, com advisory lock do Postgres.
///
/// Por que a trava: em deploy com mais de uma replica, dois processos chamam MigrateAsync no
/// mesmo instante. O EF nao serializa isso — as duas leem "__EFMigrationsHistory" vazio, as
/// duas rodam o mesmo CREATE TABLE, e a perdedora morre com 42P07 (ou pior: grava a linha de
/// history sem ter criado a tabela). O pg_advisory_lock e de SESSAO, entao precisa da MESMA
/// conexao do inicio ao fim — dai o OpenConnectionAsync explicito e o unlock no finally.
/// </summary>
public static class MigrationRunner
{
    /// <summary>
    /// Id arbitrario porem FIXO da trava. Mudar este numero anula a protecao entre versoes.
    /// </summary>
    private const long ChaveAdvisoryLock = 918273645L;

    public static async Task AplicarAsync(
        GlorificContext contexto,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(logger);

        await contexto.Database.OpenConnectionAsync(cancellationToken);
        var travado = false;

        try
        {
            // Bloqueante de proposito: a segunda replica ESPERA em vez de correr junto.
            await contexto.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_lock({ChaveAdvisoryLock})", cancellationToken);
            travado = true;

            var pendentes = (await contexto.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pendentes.Count == 0)
            {
                logger.LogInformation("Nenhuma migration pendente.");
            }
            else
            {
                logger.LogInformation(
                    "Aplicando {Quantidade} migrations: {Lista}",
                    pendentes.Count,
                    string.Join(", ", pendentes));
            }

            await contexto.Database.MigrateAsync(cancellationToken);

            // Guarda anti-drift: o history pode dizer "aplicada" com a tabela ausente.
            // E exatamente o modo de falha que deixou o worker do repo de referencia em loop
            // de 42P01 (relation does not exist) sem ninguem perceber por horas.
            // O alias "Value" e obrigatorio: SqlQuery<T> embrulha o comando em
            // SELECT s."Value" FROM (<seu sql>) AS s. Sem o alias o Postgres nomeia a
            // coluna como ?column? e o boot morre com 42703 (column s.Value does not exist).
            var schemaOk = await contexto.Database
                .SqlQuery<bool>($"SELECT (to_regclass('public.envios') IS NOT NULL) AS \"Value\"")
                .SingleAsync(cancellationToken);

            if (!schemaOk)
            {
                throw new InvalidOperationException(
                    "Schema inconsistente: __EFMigrationsHistory esta adiantado em relacao ao schema real.");
            }

            logger.LogInformation("Schema verificado. Banco pronto.");
        }
        finally
        {
            if (travado)
            {
                // Sem cancellationToken: liberar a trava nao pode ser cancelado junto com o boot,
                // senao a sessao seguinte fica esperando um lock que ninguem vai soltar.
                await contexto.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({ChaveAdvisoryLock})");
            }

            await contexto.Database.CloseConnectionAsync();
        }
    }
}
