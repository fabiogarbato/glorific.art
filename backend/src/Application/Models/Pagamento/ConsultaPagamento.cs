using Glorific.Domain.Enums;

namespace Glorific.Application.Models.Pagamento;

/// <summary>
/// Status devolvido pelo gateway na conferencia. Enum proprio, e nao
/// <see cref="StatusPagamento"/> do dominio, por causa de <see cref="Desconhecido"/>: quando o
/// provedor inventa um estado novo, o servico precisa poder NAO decidir nada em vez de mapear
/// para "Aprovado" por descuido.
/// </summary>
public enum StatusPagamentoGateway
{
    /// <summary>Provedor devolveu algo que nao sabemos mapear. Nunca confirmar pedido com isto.</summary>
    Desconhecido = 0,
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3,
    Expirado = 4,
    Cancelado = 5,
    Estornado = 6
}

/// <summary>
/// Resposta da conferencia de pagamento — a FONTE DA VERDADE do fluxo.
///
/// Falha #1 do geb-sul que nao se repete aqui: la o webhook (que nao e assinado) e o redirect
/// (que e um GET) marcavam o pedido como pago; o payment_check era chamado e o resultado
/// IGNORADO no catch. Qualquer um que descobrisse um order_nsu quitava um pedido de graca.
/// Regra: so marca Pago se este objeto vier Aprovado E
/// <see cref="ValorCentavos"/> bater com o total do pedido (falha #2).
/// </summary>
public sealed record ConsultaPagamentoInfo
{
    /// <summary>false quando o gateway nao conhece a transacao. Trate como NAO pago.</summary>
    public required bool Encontrado { get; init; }

    public StatusPagamentoGateway Status { get; init; } = StatusPagamentoGateway.Desconhecido;

    /// <summary>Valor efetivamente capturado, em centavos. Conferir contra pedidos.total.</summary>
    public int? ValorCentavos { get; init; }

    /// <summary>capture_method: "credit_card", "pix", "boleto". Vai em pagamentos.metodo.</summary>
    public string? Metodo { get; init; }

    public int? Parcelas { get; init; }

    public string? OrderNsu { get; init; }

    public string? TransactionNsu { get; init; }

    /// <summary>receipt_url — comprovante para anexar ao e-mail do cliente.</summary>
    public string? UrlComprovante { get; init; }

    public DateTime? PagoEmUtc { get; init; }

    /// <summary>Status cru do provedor, guardado para auditoria quando Status = Desconhecido.</summary>
    public string? StatusOriginal { get; init; }

    public string? RawJson { get; init; }

    /// <summary>Aprovado de fato: encontrado, status aprovado e valor presente.</summary>
    public bool Aprovado => Encontrado
        && Status == StatusPagamentoGateway.Aprovado
        && ValorCentavos is > 0;

    /// <summary>Confere valor sem margem: pagamento parcial nao libera pedido.</summary>
    public bool ValorConfere(int totalEsperadoCentavos) =>
        ValorCentavos.HasValue && ValorCentavos.Value == totalEsperadoCentavos;

    /// <summary>Traducao para o enum persistido. Desconhecido mantem Pendente.</summary>
    public StatusPagamento ParaStatusDominio() => Status switch
    {
        StatusPagamentoGateway.Aprovado => StatusPagamento.Aprovado,
        StatusPagamentoGateway.Recusado => StatusPagamento.Recusado,
        StatusPagamentoGateway.Expirado => StatusPagamento.Expirado,
        StatusPagamentoGateway.Cancelado => StatusPagamento.Cancelado,
        StatusPagamentoGateway.Estornado => StatusPagamento.Estornado,
        _ => StatusPagamento.Pendente
    };
}

/// <summary>
/// O que chega no webhook ou no redirect do cliente, ja extraido do corpo cru.
///
/// Este objeto e um AVISO, nao uma prova. O corpo nao tem assinatura na InfinitePay e o redirect
/// e uma URL que qualquer um monta. Serve unicamente para descobrir QUAL transacao conferir com
/// <see cref="ConsultaPagamentoInfo"/>; nenhum campo dele deve alimentar decisao de negocio —
/// nem o amount.
/// </summary>
public sealed record WebhookPagamentoInfo
{
    public required string OrderNsu { get; init; }

    public string? TransactionNsu { get; init; }

    /// <summary>slug/capture_method — necessario para montar a consulta de conferencia.</summary>
    public string? Slug { get; init; }

    /// <summary>Valor ANUNCIADO em centavos. Nao usar como verdade; usar apenas para log.</summary>
    public int? ValorAnunciadoCentavos { get; init; }

    public string? UrlComprovante { get; init; }

    /// <summary>
    /// Id do evento para idempotencia. Sem id nativo do provedor, o servico deriva um estavel
    /// (ex.: hash de orderNsu + transactionNsu) e grava em pagamentos_eventos.provider_event_id,
    /// cuja UNIQUE transforma reentrega em 200 imediato.
    /// </summary>
    public required string ProviderEventId { get; init; }

    /// <summary>Corpo cru, exatamente como chegou. Vai para jsonb sem reserializar.</summary>
    public required string Payload { get; init; }
}
