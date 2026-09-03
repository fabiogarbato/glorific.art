using System.Text;
using Glorific.Api.Common;
using Glorific.Application.Ports.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Glorific.Api.Configuration;

/// <summary>
/// JwtBearer. Chave simetrica HS256, emitida pela propria API.
/// </summary>
public static class AutenticacaoConfiguration
{
    /// <summary>Nome curto da claim de identidade publica (usuarios.Uuid).</summary>
    public const string ClaimSub = "sub";

    /// <summary>Nome curto da claim de papel. Casa com RequireRole das policies.</summary>
    public const string ClaimRole = "role";

    /// <param name="chaveJaValidada">
    /// A chave EXATAMENTE como saiu do RequiredSecret (ja com Trim). Recebida por parametro de
    /// proposito: reler a configuracao aqui e o bug do repo de referencia — o boot validava com
    /// Trim, o emissor lia cru, e uma env var com quebra de linha invalidava todo token emitido.
    /// </param>
    public static IServiceCollection AddAutenticacao(
        this IServiceCollection services,
        IConfiguration configuration,
        string chaveJaValidada)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveJaValidada);

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        var chaveAssinatura = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJaValidada));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                // Sem isto o handler renomeia "sub" para a URI longa de NameIdentifier e "role"
                // para a URI de Role. O servidor passa a ler um nome e o front outro — no repo
                // de referencia o front lia decoded.nameidentifier, que nunca existiu.
                opcoes.MapInboundClaims = false;

                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = chaveAssinatura,

                    // O default do framework e 5 minutos, o que mantem token morto valido por
                    // tempo demais e mascara erro de relogio do host.
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSegundos),

                    NameClaimType = ClaimSub,
                    RoleClaimType = ClaimRole
                };

                // 401 e 403 do JwtBearer saem com CORPO VAZIO por padrao. Aqui eles usam o mesmo
                // envelope do resto da API, senao o front precisa de um ramo especial de parse
                // justamente no caminho de erro mais comum.
                opcoes.Events = new JwtBearerEvents
                {
                    OnChallenge = async contexto =>
                    {
                        // Impede o handler de escrever o WWW-Authenticate e encerrar sozinho.
                        contexto.HandleResponse();

                        await RespostaErro.EscreverAsync(
                            contexto.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Autenticacao necessaria.");
                    },

                    OnForbidden = contexto => RespostaErro.EscreverAsync(
                        contexto.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Acesso negado para o seu perfil.")
                };
            });

        return services;
    }
}
