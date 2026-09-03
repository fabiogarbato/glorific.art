using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Checkout;

/// <summary>
/// Corpo de POST /api/v1/checkout.
///
/// Repare no que NAO existe aqui: usuario, precos, valor do frete e total. Tudo isso e decidido
/// no servidor. O repo de referencia aceitava o valor do frete vindo do cliente e cobrava o que
/// chegasse — trocar um numero no devtools comprava frete de graca. Aqui o cliente escolhe
/// apenas QUAL servico quer; quanto ele custa e resultado de recotacao server-side.
/// </summary>
public sealed record CheckoutRequestDto
{
    /// <summary>
    /// Endereco de entrega do proprio usuario. A posse e conferida na consulta (WHERE id_usuario),
    /// e endereco de outra pessoa devolve 404 — nunca 403, que confirmaria a existencia.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Informe o endereco de entrega.")]
    public int IdEndereco { get; init; }

    /// <summary>Id do servico do Melhor Envio escolhido na cotacao (1 PAC, 2 SEDEX...).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Escolha uma opcao de frete.")]
    public int IdServicoFrete { get; init; }

    [StringLength(60, ErrorMessage = "Codigo de cupom invalido.")]
    public string? CodigoCupom { get; init; }

    [StringLength(500, ErrorMessage = "A observacao deve ter no maximo 500 caracteres.")]
    public string? ObservacaoCliente { get; init; }
}

/// <summary>
/// Resposta do checkout. Devolve o Uuid (nao o Id inteiro) porque e ele que vai para a URL de
/// acompanhamento e para o polling de status.
/// </summary>
public sealed record CheckoutCriadoResponseDto
{
    public required string Numero { get; init; }

    public required string Uuid { get; init; }

    /// <summary>Pagina de pagamento hospedada. O front redireciona para ca.</summary>
    public string? PaymentUrl { get; init; }

    public string? QrCodePix { get; init; }

    public string? LinhaDigitavel { get; init; }

    public DateTime? ExpiraEm { get; init; }

    public int SubtotalCentavos { get; init; }

    public int DescontoCupomCentavos { get; init; }

    public int FreteCentavos { get; init; }

    public int TotalCentavos { get; init; }
}

/// <summary>
/// Alvo do polling de GET /api/v1/checkout/{uuid}/status enquanto o cliente paga.
/// Pago e derivado no servidor: o front nunca deve inferir pagamento comparando strings de status.
/// </summary>
public sealed record CheckoutStatusResponseDto
{
    public required string Uuid { get; init; }

    public required string Numero { get; init; }

    public required string StatusPedido { get; init; }

    public required string StatusPagamento { get; init; }

    /// <summary>true somente quando o gateway confirmou e o valor bateu.</summary>
    public bool Pago { get; init; }

    /// <summary>true quando nao ha mais o que esperar (pago, recusado, expirado, cancelado).</summary>
    public bool Terminal { get; init; }

    public string? PaymentUrl { get; init; }

    public DateTime? ExpiraEm { get; init; }
}
