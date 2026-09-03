using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Glorific.Api.Configuration;

/// <summary>
/// Health checks. Sem eles, orquestrador nenhum sabe a diferenca entre "processo vivo" e
/// "aplicacao funcionando" — o container fica de pe respondendo 500 em toda rota e o load
/// balancer continua mandando trafego para ele.
/// </summary>
public static class HealthChecksConfiguration
{
    /// <summary>Checks que precisam estar verdes para o container receber trafego.</summary>
    public const string TagProntidao = "ready";

    public static IServiceCollection AddHealthChecksConfigurados(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddHealthChecks()
            .AddCheck<BancoHealthCheck>(
                "banco",
                failureStatus: HealthStatus.Unhealthy,
                tags: [TagProntidao]);

        return services;
    }
}

/// <summary>
/// Conectividade com o Postgres.
///
/// Escrito a mao, sem pacote de terceiro, por dois motivos: mantem a arvore de dependencias
/// enxuta e, principalmente, usa o MESMO DbContext e a MESMA connection string da aplicacao.
/// Um check que abre conexao propria pode ficar verde enquanto o pool da aplicacao esta esgotado.
/// </summary>
public sealed class BancoHealthCheck : IHealthCheck
{
    private readonly GlorificContext _contexto;

    public BancoHealthCheck(GlorificContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conectou = await _contexto.Database.CanConnectAsync(cancellationToken);

            return conectou
                ? HealthCheckResult.Healthy("Postgres acessivel.")
                : HealthCheckResult.Unhealthy("Postgres inacessivel.");
        }
        catch (Exception excecao)
        {
            // A excecao vai para o resultado do check (consumido por orquestrador, nao pelo
            // navegador do cliente), mas o corpo publico de /health nunca a expoe.
            return HealthCheckResult.Unhealthy("Falha ao consultar o Postgres.", excecao);
        }
    }
}
