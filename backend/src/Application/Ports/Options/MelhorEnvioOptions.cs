using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "MelhorEnvio". Aponta para o MICROSERVICO integracaoMelhorEnvio, nao para a API do
/// Melhor Envio: o OAuth, a renovacao do token e o passthrough do corpo cru ficam la.
/// </summary>
public sealed class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    /// <summary>
    /// URL do microservico. Entre containers e o DNS interno (http://melhorenvio_api:8080);
    /// 127.0.0.1:5006 e loopback do HOST e nao funciona de dentro de outro container.
    /// </summary>
    [Required(ErrorMessage = "MelhorEnvio:BaseUrl e obrigatoria.")]
    [Url(ErrorMessage = "MelhorEnvio:BaseUrl precisa ser uma URL valida.")]
    public string BaseUrl { get; set; } = "http://melhorenvio_api:8080";

    /// <summary>
    /// Valor do header X-Api-Key. E all-or-nothing: quem tem a chave opera qualquer conta e
    /// qualquer rota do microservico. Vem de env, nunca do appsettings versionado.
    /// </summary>
    [Required(ErrorMessage = "MelhorEnvio:ApiKey e obrigatoria.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// accountId enviado como query em todas as rotas do microservico — a chave multi-tenant da
    /// tabela de tokens dele. O adaptador anexa sozinho; nenhum servico de negocio passa isto.
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
