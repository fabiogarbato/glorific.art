using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Glorific.Api.Configuration;

/// <summary>
/// As quatro policies do sistema mais a correcao central de autorizacao do projeto.
///
/// FallbackPolicy = RequireAuthenticatedUser(): endpoint SEM atributo nenhum passa a exigir
/// autenticacao. No repo de referencia seis controllers ficaram publicos por omissao, dois deles
/// com "// TODO: Apenas para ADMINS" pendurado. Com o fallback, esquecer o atributo vira 401 —
/// falha barulhenta — em vez de vazamento silencioso. Rota publica passa a exigir
/// [AllowAnonymous] EXPLICITO, que e uma decisao visivel na revisao de codigo.
/// </summary>
public static class AutorizacaoConfiguration
{
    public static IServiceCollection AddAutorizacao(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())

            // Usuarios, papeis, segredos e configuracao da loja.
            .AddPolicy(PoliticasAutorizacao.SomenteAdmin, politica =>
                politica.RequireRole(Roles.Admin))

            // Catalogo, preco, estoque, cupom e moderacao.
            .AddPolicy(PoliticasAutorizacao.GestaoCatalogo, politica =>
                politica.RequireRole(Roles.Admin, Roles.Gerente))

            // Pedidos, etiquetas e rastreio.
            .AddPolicy(PoliticasAutorizacao.Expedicao, politica =>
                politica.RequireRole(Roles.Admin, Roles.Gerente, Roles.Operador))

            // Porta de entrada do painel: qualquer papel administrativo serve. A autorizacao
            // fina continua sendo por policy na classe do controller.
            .AddPolicy(PoliticasAutorizacao.PainelAdmin, politica =>
                politica.RequireRole([.. Roles.Administrativos]));

        return services;
    }
}
