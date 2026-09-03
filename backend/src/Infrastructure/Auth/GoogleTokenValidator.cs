using Glorific.Application.Exceptions;
using Glorific.Application.Models.Auth;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Auth;

/// <summary>
/// Adaptador do fluxo Google Identity Services: o front obtem o id_token e o back valida.
///
/// A validacao acontece em DUAS camadas, de proposito:
///
/// 1. <see cref="GoogleIdTokenGuardas"/> — nossas, baratas e testaveis sem rede: o emissor tem de
///    ser accounts.google.com (com ou sem esquema) e a audience tem de ser o NOSSO ClientId.
///    Elas nao provam nada sozinhas (ninguem confere assinatura ali), mas tiram a regra mais cara
///    do fluxo — a audience — de dentro do default de uma dependencia externa.
/// 2. <see cref="GoogleJsonWebSignature"/> — a autoridade: confere a assinatura contra o JWKS do
///    Google (com cache interno da biblioteca), o issuer, a expiracao e, de novo, a AUDIENCE.
///    Sem conferir aud, um id_token emitido para QUALQUER outro aplicativo Google seria aceito
///    aqui como login valido, e obter um desses e trivial.
///
/// Nenhum tipo da Google.Apis.Auth atravessa a porta: o que sai e um record da Application.
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    /// <summary>A unica mensagem que chega ao navegador quando a loja nao configurou o Google.</summary>
    public const string MensagemNaoConfigurado =
        "O login com Google nao esta configurado nesta loja. Entre com e-mail e senha.";

    private readonly IOptionsMonitor<GoogleOptions> _opcoes;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(
        IOptionsMonitor<GoogleOptions> opcoes,
        ILogger<GoogleTokenValidator> logger)
    {
        _opcoes = opcoes;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoogleIdentityInfo?> ValidarAsync(string idToken, CancellationToken ct = default)
    {
        var opcoes = _opcoes.CurrentValue;

        // A conferencia de CONFIGURACAO vem antes de qualquer coisa, inclusive do token vazio.
        // Se a loja nao tem ClientId, o motivo da falha e sempre esse — devolver "token
        // invalido" aqui mandaria todo mundo procurar bug no front.
        if (opcoes.NaoConfigurado)
        {
            throw new IntegracaoNaoConfiguradaException(
                "Google",
                MensagemNaoConfigurado,
                "Google:ClientId nao configurado (ausente, com o placeholder do appsettings ou sem o sufixo " +
                $"'{GoogleOptions.SufixoClientIdGoogle}'). Defina Google__ClientId para habilitar o login com Google.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        ct.ThrowIfCancellationRequested();

        var clientId = opcoes.ClientId.Trim();

        // Guardas nossas: emissor e audience. So reprovam — nunca aprovam nada por conta propria.
        if (!GoogleIdTokenGuardas.PodeSeguirParaValidacao(idToken, clientId, out var motivo))
        {
            _logger.LogDebug("id_token do Google recusado antes da validacao de assinatura: {Motivo}.", motivo);
            return null;
        }

        var tolerancia = TimeSpan.FromSeconds(opcoes.ToleranciaRelogioSegundos);

        var configuracao = new GoogleJsonWebSignature.ValidationSettings
        {
            // A audience esperada e o NOSSO ClientId, o mesmo que o front usa no GSI.
            Audience = new[] { clientId },

            // Tolerancia pequena e explicita: o default mascara relogio errado do host, e a
            // ausencia dela recusa token legitimo por um segundo de diferenca.
            IssuedAtClockTolerance = tolerancia,
            ExpirationTimeClockTolerance = tolerancia
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, configuracao);

            // Cinto e suspensorio: a biblioteca ja conferiu iss e aud, mas as duas regras que
            // decidem "este token e nosso" ficam afirmadas AQUI, no nosso codigo, tambem depois.
            if (!GoogleIdTokenGuardas.EmissorAceito(payload.Issuer))
            {
                _logger.LogWarning(
                    "id_token aceito pela biblioteca com emissor inesperado {Emissor}. Recusado.",
                    payload.Issuer);

                return null;
            }

            if (!GoogleIdTokenGuardas.AudienceAceita(LerAudience(payload), clientId))
            {
                _logger.LogWarning("id_token aceito pela biblioteca com audience diferente do ClientId. Recusado.");
                return null;
            }

            return new GoogleIdentityInfo
            {
                // O sub e a identidade estavel. O e-mail de uma conta Google pode mudar.
                Subject = payload.Subject ?? string.Empty,
                Email = payload.Email ?? string.Empty,

                // "is true" cobre tanto bool quanto bool? conforme a versao da biblioteca, e o
                // ausente e tratado como NAO verificado, que e o lado seguro do erro.
                EmailVerificado = payload.EmailVerified is true,

                Nome = payload.Name,
                FotoUrl = payload.Picture
            };
        }
        catch (InvalidJwtException excecao)
        {
            // Token invalido, expirado, de outra audience ou adulterado. E caso ESPERADO no
            // endpoint de login: devolve null e o servico traduz para 401. Nivel Debug de
            // proposito — logar como erro encheria o alerta com gente digitando errado.
            _logger.LogDebug(excecao, "id_token do Google recusado na validacao.");
            return null;
        }
        catch (Exception excecao) when (excecao is FormatException or ArgumentException)
        {
            // String que nem chega a ser um JWT (base64 quebrado, corpo colado errado).
            _logger.LogDebug(excecao, "id_token do Google malformado.");
            return null;
        }

        // Falha de rede ao buscar o JWKS NAO e capturada de proposito: ela propaga, vira 500 e
        // aparece no alerta. Traduzi-la para null diria ao cliente "sua conta esta errada"
        // quando o problema e a nossa saida para a internet.
    }

    /// <summary>
    /// A claim aud do payload ja validado. A biblioteca expoe o valor cru (string ou lista
    /// separada por espaco, conforme o token), por isso a leitura e defensiva.
    /// </summary>
    private static IReadOnlyList<string> LerAudience(GoogleJsonWebSignature.Payload payload)
    {
        var bruto = payload.AudienceAsList;

        return bruto is null
            ? []
            : [.. bruto.Where(audience => !string.IsNullOrWhiteSpace(audience))];
    }
}
