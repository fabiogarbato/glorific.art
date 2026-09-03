using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Entities.Estoque;

/// <summary>
/// Estoque por SKU, nunca por produto.
///
/// Reserva SOFT: o checkout incrementa QuantidadeReservada; Quantidade continua sendo
/// o estoque FISICO. O repo de referencia decrementa direto na reserva, e enquanto o
/// pagamento esta pendente o painel mostra estoque errado (a peca esta na prateleira
/// mas sumiu do relatorio) e o cancelamento tem que devolver sem distinguir
/// devolucao-de-reserva de entrada real.
/// </summary>
public class EstoqueVariacao : BaseEntity
{
    public int IdVariacao { get; set; }
    public ProdutoVariacao Variacao { get; set; } = null!;

    /// <summary>Estoque fisico, o que existe na prateleira.</summary>
    public int Quantidade { get; set; }

    /// <summary>Comprometido em checkout aguardando pagamento.</summary>
    public int QuantidadeReservada { get; set; }

    /// <summary>Limite para o alerta de estoque baixo no painel.</summary>
    public int QuantidadeMinima { get; set; }

    public string? Localizacao { get; set; }
    public DateTime? DataUltimaMovimentacao { get; set; }

    /// <summary>O que pode ser vendido agora.</summary>
    public int Disponivel => Quantidade - QuantidadeReservada;
}
