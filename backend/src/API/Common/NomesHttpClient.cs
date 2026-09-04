namespace Glorific.Api.Common;

/// <summary>
/// Nomes dos HttpClients tipados registrados no boot.
///
/// Constante, e nao string repetida: o adaptador da Infrastructure resolve o client pelo nome,
/// e um typo ali devolve um HttpClient DEFAULT — sem BaseAddress, sem header de autenticacao,
/// sem timeout. A falha aparece como 404 do host errado, nao como erro de configuracao.
/// </summary>
public static class NomesHttpClient
{
    /// <summary>Microservico integracaoMelhorEnvio (nao a API do Melhor Envio).</summary>
    public const string MelhorEnvio = "melhor-envio";

    /// <summary>Gateway de pagamento InfinitePay.</summary>
    public const string InfinitePay = "infinite-pay";

    /// <summary>Consulta publica de CEP.</summary>
    public const string ViaCep = "via-cep";

    /// <summary>Geracao de descricao de produto com IA (visao + texto).</summary>
    public const string OpenAi = "open-ai";
}
