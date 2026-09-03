namespace Glorific.Api.Configuration;

/// <summary>
/// CORS da loja. Lista fechada de origens, com curinga apenas de subdominio, avaliada pelo
/// <see cref="CorsOriginMatcher"/>.
/// </summary>
public static class CorsConfiguration
{
    public const string NomePolitica = "GlorificCors";

    /// <summary>Secao do appsettings: "Cors": { "Origens": [ ... ] }.</summary>
    public const string SecaoOrigens = "Cors:Origens";

    public static IServiceCollection AddCorsConfigurado(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment ambiente)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(ambiente);

        var origens = configuration.GetSection(SecaoOrigens).Get<string[]>() ?? [];

        // Localhost liberado SO em desenvolvimento. Em producao, uma pagina servida da maquina
        // de um atacante em http://localhost nao pode falar com a API do cliente.
        var matcher = new CorsOriginMatcher(origens, permitirLocalhost: ambiente.IsDevelopment());

        // Singleton para o boot poder logar as entradas invalidas e para o teste de fronteira
        // resolver exatamente o mesmo matcher que a politica usa.
        services.AddSingleton(matcher);

        services.AddCors(opcoes =>
        {
            opcoes.AddPolicy(NomePolitica, politica =>
            {
                politica
                    .SetIsOriginAllowed(matcher.Corresponde)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    // O refresh token vive num cookie httpOnly, entao o navegador PRECISA
                    // enviar credencial no fetch. Isso obriga origem exata (nunca "*"), que e
                    // justamente o que o matcher garante.
                    .AllowCredentials()
                    // Sem isso o navegador refaz o preflight OPTIONS a cada requisicao.
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            });
        });

        return services;
    }

    /// <summary>
    /// Loga a configuracao efetiva de CORS no boot.
    ///
    /// Existe porque erro de CORS e o problema mais caro de diagnosticar do outro lado: o
    /// navegador so diz "bloqueado", sem contar o motivo. Ver a lista aceita e a lista recusada
    /// no log do servidor resolve em segundos.
    /// </summary>
    public static void LogarConfiguracaoCors(this WebApplication app)
    {
        var matcher = app.Services.GetRequiredService<CorsOriginMatcher>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cors");

        if (matcher.EntradasInvalidas.Count > 0)
        {
            logger.LogWarning(
                "CORS: {Quantidade} entrada(s) IGNORADAS por formato invalido: {Entradas}",
                matcher.EntradasInvalidas.Count,
                string.Join(", ", matcher.EntradasInvalidas));
        }

        if (!matcher.TemAlgumaOrigem)
        {
            logger.LogWarning(
                "CORS: nenhuma origem valida configurada. Todo pedido de navegador sera bloqueado. " +
                "Preencha Cors__Origens__0, Cors__Origens__1, ...");
            return;
        }

        logger.LogInformation(
            "CORS: exatas [{Exatas}] curingas [{Curingas}]",
            string.Join(", ", matcher.OrigensExatas),
            string.Join(", ", matcher.OrigensCuringa));
    }
}
