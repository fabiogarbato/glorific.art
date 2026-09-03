using Glorific.Domain.Enums;

namespace Glorific.Application.Models.MelhorEnvio;

/// <summary>
/// Item da resposta de POST {ME}/api/shipment/tracking (o ME devolve um mapa id -> objeto).
///
/// Regra do consumidor: o status local so PROMOVE, nunca regride
/// (if (promovido &gt; envio.Status)). O ME reordena eventos e reenvia estados antigos; deixar
/// regredir faz um pedido entregue voltar para "postado" na tela do cliente.
/// </summary>
public sealed record RastreioResultado
{
    public required string MeOrderId { get; init; }

    public string? Protocolo { get; init; }

    /// <summary>Status cru do ME ("posted", "delivered", "canceled"...). Guardar sempre.</summary>
    public string? StatusOriginal { get; init; }

    /// <summary>
    /// Equivalente no nosso dominio, quando o adaptador consegue mapear. Null = status
    /// desconhecido: registrar o evento e NAO mexer no status local.
    /// </summary>
    public StatusEnvio? StatusEquivalente { get; init; }

    /// <summary>tracking — codigo da transportadora (o que o cliente cola no site dos Correios).</summary>
    public string? CodigoRastreio { get; init; }

    /// <summary>melhorenvio_tracking — pagina de rastreio hospedada pelo ME.</summary>
    public string? UrlRastreio { get; init; }

    public DateTime? PostadoEmUtc { get; init; }

    public DateTime? EntregueEmUtc { get; init; }

    public IReadOnlyList<RastreioEventoInfo> Eventos { get; init; } = [];

    public string? RawJson { get; init; }
}

/// <summary>Linha do historico de rastreio, para gravar em envios_eventos.</summary>
public sealed record RastreioEventoInfo
{
    public DateTime? DataUtc { get; init; }

    public string? Descricao { get; init; }

    public string? Local { get; init; }
}

/// <summary>
/// Entrada de POST {ME}/api/shipment/cancel.
/// MotivoId e SEMPRE "2" em integracao — o ME so aceita a lista fechada dele e "2" e o
/// generico de desistencia. Chamar fora da transacao: cancelar e I/O de rede.
/// </summary>
public sealed record CancelamentoEtiquetaRequisicao
{
    public required string MeOrderId { get; init; }

    public string MotivoId { get; init; } = "2";

    public string? Descricao { get; init; }
}

public sealed record CancelamentoEtiquetaResultado
{
    public required bool Sucesso { get; init; }

    public string? Mensagem { get; init; }

    public string? RawJson { get; init; }
}
