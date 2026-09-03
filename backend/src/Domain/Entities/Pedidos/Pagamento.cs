using Glorific.Domain.Common;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// Um pagamento por pedido (unico no banco). Os identificadores do provedor sao guardados em
/// dois campos separados porque o webhook chega ora com o id do pedido no gateway, ora com o id
/// da cobranca — procurar por um so deixa evento orfao.
///
/// RawUltimaResposta guarda o payload cru em jsonb: quando o gateway muda contrato sem avisar,
/// e a unica forma de reconstruir o que aconteceu sem pedir log para o suporte deles.
/// </summary>
public class Pagamento : BaseEntity
{
    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public required string Provedor { get; set; }

    /// <summary>pix, credit_card, boleto — como o provedor nomeia.</summary>
    public string? Metodo { get; set; }

    public StatusPagamento Status { get; set; } = StatusPagamento.Pendente;

    /// <summary>Valor em centavos, o mesmo que foi enviado ao gateway.</summary>
    public int ValorCentavos { get; set; }

    public int? Parcelas { get; set; }

    public string? ProviderOrderId { get; set; }
    public string? ProviderChargeId { get; set; }

    public string? PaymentUrl { get; set; }
    public string? QrCodePix { get; set; }
    public string? LinhaDigitavel { get; set; }

    /// <summary>Prazo do pix ou do boleto. E o gatilho do worker que cancela e libera a reserva.</summary>
    public DateTime? ExpiraEm { get; set; }

    public string? RawUltimaResposta { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataConfirmacao { get; set; }

    public ICollection<PagamentoEvento> Eventos { get; set; } = [];
}
