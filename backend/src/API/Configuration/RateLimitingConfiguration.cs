using System.Globalization;
using System.Threading.RateLimiting;
using Glorific.Api.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace Glorific.Api.Configuration;

/// <summary>Nomes das policies de rate limit. Constante para nao virar string magica no atributo.</summary>
public static class PoliticasRateLimit
{
    /// <summary>Login, registro, refresh, recuperacao de senha. Alvo classico de forca bruta.</summary>
    public const string Auth = "rl-auth";

    /// <summary>
    /// Cotacao de frete. Cada chamada vira uma consulta paga no Melhor Envio: um bot cotando em
    /// loop nao derruba a loja, mas queima a cota da conta.
    /// </summary>
    public const string Frete = "rl-frete";

    /// <summary>Consultas de CEP e outros proxies de terceiro.</summary>
    public const string Consulta = "rl-consulta";
}

/// <summary>
/// Rate limiting por IP, com janela fixa.
///
/// Particao por IP e nao global: janela global transforma um unico abusador em indisponibilidade
/// para todos os clientes. E o IP sai do RemoteIpAddress DEPOIS do UseForwardedHeaders, senao
/// atras do proxy todos os clientes compartilhariam a mesma particao (o IP do proxy) e a loja
/// inteira cairia na cota de uma pessoa.
/// </summary>
public static class RateLimitingConfiguration
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var secao = configuration.GetSection("RateLimit");

        var limiteAuth = secao.GetValue("AuthPorMinuto", 20);
        var limiteFrete = secao.GetValue("FretePorMinuto", 30);
        var limiteConsulta = secao.GetValue("ConsultaPorMinuto", 60);

        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opcoes.AddPolicy(PoliticasRateLimit.Auth, contexto => PorIp(contexto, limiteAuth));
            opcoes.AddPolicy(PoliticasRateLimit.Frete, contexto => PorIp(contexto, limiteFrete));
            opcoes.AddPolicy(PoliticasRateLimit.Consulta, contexto => PorIp(contexto, limiteConsulta));

            opcoes.OnRejected = async (contexto, cancellationToken) =>
            {
                // Retry-After so e informado quando o limitador sabe a janela; sem ele o cliente
                // fica tentando as cegas e mantem a cota estourada.
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await RespostaErro.EscreverAsync(
                    contexto.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Muitas requisicoes em pouco tempo. Aguarde alguns instantes e tente novamente.");
            };
        });

        return services;
    }

    private static RateLimitPartition<string> PorIp(HttpContext contexto, int limitePorMinuto)
    {
        var chave = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limitePorMinuto,
            Window = TimeSpan.FromMinutes(1),
            // Fila zero: enfileirar requisicao de abusador consome memoria do servidor para
            // atende-lo depois. Recusar na hora e mais barato e mais honesto com o cliente.
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }
}
