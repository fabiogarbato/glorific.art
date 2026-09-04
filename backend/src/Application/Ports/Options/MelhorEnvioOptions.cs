using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "MelhorEnvio". Fala DIRETO com a API do Melhor Envio (OAuth2 "authorization code") —
/// nao existe mais um microservico intermediario. O adaptador (MelhorEnvioClient) guarda e
/// renova o token sozinho, usando IContaMelhorEnvioRepository.
/// </summary>
public sealed class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    /// <summary>
    /// Raiz da API do Melhor Envio. Sandbox: https://sandbox.melhorenvio.com.br.
    /// Producao: https://melhorenvio.com.br.
    /// </summary>
    [Required(ErrorMessage = "MelhorEnvio:BaseUrl e obrigatoria.")]
    [Url(ErrorMessage = "MelhorEnvio:BaseUrl precisa ser uma URL valida.")]
    public string BaseUrl { get; set; } = "https://sandbox.melhorenvio.com.br";

    /// <summary>Client ID do aplicativo cadastrado na Area Dev do Melhor Envio.</summary>
    [Required(ErrorMessage = "MelhorEnvio:ClientId e obrigatorio.")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret do mesmo aplicativo. Vem de env, nunca do appsettings versionado.</summary>
    [Required(ErrorMessage = "MelhorEnvio:ClientSecret e obrigatorio.")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Precisa ser IDENTICA a URL de redirecionamento cadastrada no aplicativo do Melhor Envio
    /// (hoje: https://hml.glorific.art/) — o ME recusa o code se a URL enviada na troca por
    /// token divergir, mesmo que so na barra final.
    /// </summary>
    [Required(ErrorMessage = "MelhorEnvio:RedirectUri e obrigatoria.")]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Escopos pedidos na autorizacao. Precisam cobrir todo o fluuxo G.4 (cotar, carrinho,
    /// comprar, gerar, imprimir, rastrear).
    /// </summary>
    public string Escopo { get; set; } =
        "cart-read cart-write shipping-calculate shipping-checkout shipping-generate " +
        "shipping-print shipping-tracking shipping-cancel";

    /// <summary>
    /// accountId interno (rotulo, nao credencial) — hoje sempre "glorific", chave da linha unica
    /// em contas_melhor_envio.
    /// </summary>
    [Required]
    public string ContaId { get; set; } = "glorific";

    [Range(5, 120, ErrorMessage = "MelhorEnvio:TimeoutSegundos deve estar entre 5 e 120.")]
    public int TimeoutSegundos { get; set; } = 30;

    /// <summary>
    /// Saldo minimo aceitavel na carteira, em centavos. Abaixo disso o healthcheck alerta: a
    /// compra da etiqueta debita da carteira e falha DEPOIS do cliente ja ter pago.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int SaldoMinimoAlertaCentavos { get; set; } = 5000;
}
