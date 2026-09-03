using Glorific.Domain.Common;
using Glorific.Domain.Entities.Estoque;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// O SKU REAL — o que tem estoque, o que tem peso, o que e vendido.
///
/// Peso e dimensoes sao NOT NULL por contrato: sem eles nao existe cotacao no Melhor Envio
/// (products[].weight/width/height/length sao obrigatorios em POST /api/shipment/calculate).
/// Alem disso "Vestido P" e "Vestido GG" tem peso 15-20% diferente — peso no Produto
/// significa frete errado em todo pedido de peca grande.
/// </summary>
public class ProdutoVariacao : BaseEntity, IAuditable
{
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    /// <summary>Unico globalmente.</summary>
    public required string Sku { get; set; }

    public int IdTamanho { get; set; }
    public Tamanho Tamanho { get; set; } = null!;

    public int IdCor { get; set; }
    public Cor Cor { get; set; } = null!;

    /// <summary>Override em centavos. Null herda Produto.PrecoBaseCentavos.</summary>
    public int? PrecoCentavos { get; set; }

    public string? CodigoBarras { get; set; }

    public int PesoGramas { get; set; }
    public decimal AlturaCm { get; set; }
    public decimal LarguraCm { get; set; }
    public decimal ComprimentoCm { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public EstoqueVariacao? Estoque { get; set; }

    /// <summary>Preco efetivo desta variacao, em centavos.</summary>
    public int PrecoEfetivoCentavos => PrecoCentavos ?? Produto?.PrecoBaseCentavos ?? 0;
}
