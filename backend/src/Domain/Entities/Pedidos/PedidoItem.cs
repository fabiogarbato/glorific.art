using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// TUDO aqui e snapshot — esta e a armadilha numero 2 do modelo.
///
/// O repo de referencia gravava apenas IdProduto + quantidade + valor e dependia de Include para
/// exibir nome e foto, com IgnoreQueryFilters para achar produto desativado. Consequencia:
/// renomear um produto reescreve o historico de todos os pedidos antigos, e trocar a foto muda
/// o recibo de um pedido de dois anos atras.
///
/// Aqui a linha e imutavel e autossuficiente. As FKs IdVariacao e IdProduto ficam SO para
/// relatorio de curva ABC — nenhuma tela de cliente depende delas.
/// </summary>
public class PedidoItem : BaseEntity
{
    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int IdVariacao { get; set; }
    public ProdutoVariacao Variacao { get; set; } = null!;

    /// <summary>Denormalizado para o relatorio nao precisar passar pela variacao.</summary>
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public required string SkuSnapshot { get; set; }
    public required string NomeProdutoSnapshot { get; set; }
    public required string TamanhoSnapshot { get; set; }
    public required string CorSnapshot { get; set; }

    /// <summary>A foto do recibo. Congelada para nao mudar quando o admin troca a galeria.</summary>
    public string? ImagemUrlSnapshot { get; set; }

    public int Quantidade { get; set; }

    /// <summary>Preco em centavos NO INSTANTE DA COMPRA. Nunca recalculado.</summary>
    public int PrecoUnitarioCentavos { get; set; }

    public int DescontoUnitarioCentavos { get; set; }

    /// <summary>Congela o peso usado na cotacao daquele dia.</summary>
    public int PesoGramasSnapshot { get; set; }

    /// <summary>
    /// Gravado, nao calculado: quantidade x (preco - desconto) com arredondamento de cupom
    /// percentual pode divergir de centavos do que o gateway efetivamente cobrou, e o que
    /// vale e o valor cobrado.
    /// </summary>
    public int TotalLinhaCentavos { get; set; }
}
