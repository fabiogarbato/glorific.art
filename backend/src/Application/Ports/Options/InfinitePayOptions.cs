using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "InfinitePay".
///
/// Particularidade do provedor: NAO existe chave secreta nem assinatura HMAC. A identificacao da
/// conta e o handle, que e publico (o @ do lojista). Consequencia direta: nada que chegue de
/// fora citando o handle prova coisa alguma — toda confirmacao passa obrigatoriamente pelo
/// endpoint de conferencia.
/// </summary>
public sealed class InfinitePayOptions
{
    public const string SectionName = "InfinitePay";

    /// <summary>O @ da loja, sem arroba. Ex.: "glorific".</summary>
    [Required(ErrorMessage = "InfinitePay:Handle e obrigatorio.")]
    public string Handle { get; set; } = string.Empty;

    [Required]
    [Url(ErrorMessage = "InfinitePay:BaseUrl precisa ser uma URL valida.")]
    public string BaseUrl { get; set; } = "https://api.infinitepay.io";

    /// <summary>Caminho de criacao do link de checkout.</summary>
    [Required]
    public string CheckoutPath { get; set; } = "/invoices/public/checkout/links";

    /// <summary>Caminho da conferencia — a fonte da verdade do pagamento.</summary>
    [Required]
    public string PaymentCheckPath { get; set; } = "/invoices/public/checkout/payment_check";

    [Range(5, 120, ErrorMessage = "InfinitePay:TimeoutSegundos deve estar entre 5 e 120.")]
    public int TimeoutSegundos { get; set; } = 30;

    /// <summary>
    /// Minutos de validade da cobranca antes de o worker cancelar o pedido e devolver a reserva
    /// de estoque. Sem isso, carrinho abandonado depois do link gerado prende estoque para sempre.
    /// </summary>
    [Range(5, 10080, ErrorMessage = "InfinitePay:ExpiracaoMinutos deve estar entre 5 e 10080.")]
    public int ExpiracaoMinutos { get; set; } = 1440;
}
