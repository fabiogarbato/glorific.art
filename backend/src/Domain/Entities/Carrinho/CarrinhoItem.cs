using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Entities.Carrinho;

/// <summary>
/// Item do carrinho aponta para a VARIACAO, nunca para o produto: quem tem preco, peso e
/// estoque e o SKU.
/// </summary>
public class CarrinhoItem : BaseEntity
{
    public int IdCarrinho { get; set; }
    public Carrinho Carrinho { get; set; } = null!;

    public int IdVariacao { get; set; }
    public ProdutoVariacao Variacao { get; set; } = null!;

    public int Quantidade { get; set; }

    /// <summary>
    /// Preco em centavos no instante em que o item entrou. Nao e o preco cobrado — serve para
    /// detectar divergencia e avisar o cliente antes do checkout recotar tudo.
    /// </summary>
    public int PrecoUnitarioSnapshotCentavos { get; set; }

    public DateTime DataAdicao { get; set; }
}
