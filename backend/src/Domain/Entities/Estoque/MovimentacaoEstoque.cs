using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;

namespace Glorific.Domain.Entities.Estoque;

/// <summary>
/// Ledger imutavel de estoque. QuantidadeAntes e QuantidadeDepois sao gravados na linha
/// para auditar divergencia sem replay do log inteiro — o repo de referencia so grava o delta.
/// </summary>
public class MovimentacaoEstoque : BaseEntity
{
    public int IdVariacao { get; set; }
    public ProdutoVariacao Variacao { get; set; } = null!;

    public int IdMovimento { get; set; }
    public MovimentoEstoque Movimento { get; set; } = null!;

    /// <summary>Sinalizada: positiva entrada, negativa saida.</summary>
    public int Quantidade { get; set; }

    public int QuantidadeAntes { get; set; }
    public int QuantidadeDepois { get; set; }

    public int? IdPedido { get; set; }
    public Pedido? Pedido { get; set; }

    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }

    public string? Observacao { get; set; }
    public DateTime DataMovimentacao { get; set; }
}
