using Microsoft.OpenApi;

namespace Glorific.Api.Configuration;

/// <summary>
/// Swagger/OpenAPI. Documentado uma vez, ligado so fora de producao.
/// </summary>
public static class SwaggerConfiguration
{
    private const string EsquemaBearer = "Bearer";

    public static IServiceCollection AddSwaggerConfigurado(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(opcoes =>
        {
            opcoes.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Glorific API",
                Version = "v1",
                Description =
                    "API da loja glorific.art. Todas as rotas sao versionadas em /api/v1. " +
                    "Erros usam o envelope unico { statusCode, error, traceId, errors? }."
            });

            opcoes.AddSecurityDefinition(EsquemaBearer, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Cole apenas o access token; o prefixo Bearer e adicionado pelo Swagger."
            });

            // AddSecurityRequirement, e nao so a definition: sem o requirement o botao Authorize
            // existe mas o token nao e enviado nas chamadas. Foi o que aconteceu no repo de
            // referencia, e cada teste manual de rota protegida virava um 401 inexplicavel.
            opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(EsquemaBearer, documento), new List<string>() }
            });
        });

        return services;
    }

    /// <summary>
    /// Liga a UI. O chamador decide o ambiente — em producao o Swagger fica DESLIGADO: ele
    /// publica o mapa completo da superficie de ataque, incluindo rotas administrativas.
    /// </summary>
    public static WebApplication UseSwaggerConfigurado(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(opcoes =>
        {
            opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "Glorific API v1");
            opcoes.DocumentTitle = "Glorific API";
        });

        return app;
    }
}
