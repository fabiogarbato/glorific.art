using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// Trilha de mudanca de status. E o que responde "quem cancelou este pedido e quando" —
/// pergunta que o status atual, sozinho, nunca responde.
/// </summary>
public class PedidoHistorico : BaseEntity
{
    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    /// <summary>Null na criacao do pedido, quando nao existe status anterior.</summary>
    public StatusPedido? StatusAnterior { get; set; }

    public StatusPedido StatusNovo { get; set; }

    /// <summary>Null significa sistema: worker ou webhook do gateway.</summary>
    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }

    public string? Observacao { get; set; }
    public DateTime DataAlteracao { get; set; }
}
