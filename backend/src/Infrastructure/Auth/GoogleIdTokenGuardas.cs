using System.Text;
using System.Text.Json;

namespace Glorific.Infrastructure.Auth;

/// <summary>
/// Conferencia EXPLICITA de <c>iss</c> e <c>aud</c> do id_token do Google, feita por nos antes de
/// entregar o token a biblioteca.
///
/// POR QUE EXISTE, se a Google.Apis.Auth ja confere as duas coisas:
///
/// 1. Porque "a biblioteca confere" e uma afirmacao que ninguem nesta base consegue TESTAR sem
///    rede. Um token com audience de outro aplicativo e um token com audience correta caem os
///    dois em "null" — um por audience, outro por assinatura — e o teste passa sem provar nada.
///    Aqui a regra e nossa, e um teste offline consegue afirmar POR QUE o token foi recusado.
/// 2. Porque a audience errada e a falha classica que permite entrar como qualquer pessoa:
///    obter um id_token valido emitido para OUTRO aplicativo Google e trivial. Uma regra tao
///    cara nao deveria depender exclusivamente do default de uma dependencia externa, que muda
///    de versao sem avisar.
///
/// O QUE ISTO NAO E: verificacao de seguranca suficiente. Nada aqui confere assinatura — o corpo
/// do JWT e lido SEM confianca nenhuma, so para descartar cedo o que ja sabemos que nao serve.
/// Quem prova que o Google assinou continua sendo a biblioteca, sempre, logo depois.
///
/// Por isso a funcao so sabe REPROVAR: se o corpo nao puder ser lido, ela deixa passar e a
/// biblioteca decide. Fechar aqui por falha de parse duplicaria a decisao em dois lugares.
/// </summary>
public static class GoogleIdTokenGuardas
{
    /// <summary>
    /// Os dois unicos emissores de id_token do Google. Ambos aparecem em tokens reais — a forma
    /// sem esquema e historica e continua sendo emitida, entao aceitar so a com "https://"
    /// recusaria login legitimo.
    /// </summary>
    public static readonly IReadOnlyList<string> EmissoresAceitos =
    [
        "accounts.google.com",
        "https://accounts.google.com"
    ];

    /// <summary>Comparacao exata, sensivel a caixa: "iss" e um identificador, nao texto livre.</summary>
    public static bool EmissorAceito(string? emissor) =>
        emissor is not null && EmissoresAceitos.Contains(emissor, StringComparer.Ordinal);

    /// <summary>
    /// A audience do token tem de ser o NOSSO client id.
    ///
    /// Aceita <c>aud</c> como string ou como array (o JWT permite os dois; o Google emite string).
    /// Comparacao ordinal: client id nao tem variacao de caixa.
    /// </summary>
    public static bool AudienceAceita(IReadOnlyList<string> audiences, string clientId) =>
        !string.IsNullOrWhiteSpace(clientId) &&
        audiences.Contains(clientId.Trim(), StringComparer.Ordinal);

    /// <summary>
    /// Le <c>iss</c> e <c>aud</c> do corpo do JWT SEM confiar neles e responde se vale a pena
    /// continuar.
    /// </summary>
    /// <param name="motivo">
    /// Preenchido apenas quando o retorno e false. Vai para o log de Debug — nunca para a
    /// resposta HTTP, que continua sendo um 401 generico.
    /// </param>
    /// <returns>
    /// false SOMENTE quando o corpo foi lido com sucesso e a claim contradiz a regra.
    /// true quando esta tudo certo OU quando nao deu para ler o corpo — nesse caso a palavra
    /// final e da biblioteca, que confere assinatura, iss, aud e validade de novo.
    /// </returns>
    public static bool PodeSeguirParaValidacao(string idToken, string clientId, out string motivo)
    {
        motivo = string.Empty;

        if (!TentarLerCorpo(idToken, out var corpo))
            return true;

        using (corpo)
        {
            var raiz = corpo.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object)
                return true;

            if (raiz.TryGetProperty("iss", out var iss) &&
                iss.ValueKind == JsonValueKind.String &&
                !EmissorAceito(iss.GetString()))
            {
                motivo = "emissor (iss) fora da lista aceita do Google";
                return false;
            }

            var audiences = LerAudiences(raiz);

            if (audiences.Count > 0 && !AudienceAceita(audiences, clientId))
            {
                // A falha que este ramo impede: id_token legitimo, assinado pelo Google, valido,
                // porem emitido para OUTRO aplicativo. Sem conferir aud ele seria aceito como
                // login nosso — e conseguir um desses e trivial.
                motivo = "audience (aud) emitida para outro aplicativo";
                return false;
            }
        }

        return true;
    }

    /// <summary>aud como string simples ou como array de strings. Qualquer outra forma e ignorada.</summary>
    private static IReadOnlyList<string> LerAudiences(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("aud", out var aud))
            return [];

        return aud.ValueKind switch
        {
            JsonValueKind.String => [aud.GetString() ?? string.Empty],
            JsonValueKind.Array =>
            [
                .. aud.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
            ],
            _ => []
        };
    }

    /// <summary>
    /// Segundo segmento do JWT, decodificado de base64url. Nada aqui lanca: entrada malformada
    /// e caso esperado no endpoint de login e nao pode virar 500.
    /// </summary>
    private static bool TentarLerCorpo(string idToken, out JsonDocument corpo)
    {
        corpo = null!;

        if (string.IsNullOrWhiteSpace(idToken))
            return false;

        var partes = idToken.Split('.');

        if (partes.Length != 3)
            return false;

        try
        {
            var bytes = DecodificarBase64Url(partes[1]);
            corpo = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            return true;
        }
        catch (Exception excecao) when (excecao is FormatException or JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static byte[] DecodificarBase64Url(string valor)
    {
        var normalizado = valor.Replace('-', '+').Replace('_', '/');

        // Base64url descarta o padding; Convert.FromBase64String exige multiplo de 4.
        normalizado += (normalizado.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Segmento base64url com comprimento invalido.")
        };

        return Convert.FromBase64String(normalizado);
    }
}
