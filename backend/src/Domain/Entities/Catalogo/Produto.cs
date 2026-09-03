using Glorific.Domain.Common;
using Glorific.Domain.Entities.Social;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// A PECA ("Vestido Midi Linho"), nao a unidade vendavel. Quem tem estoque, peso e
/// e efetivamente vendido e a ProdutoVariacao (o SKU).
/// PrecoBase serve para listagem e como default da variacao.
/// </summary>
public class Produto : BaseEntity, IAuditable
{
    public required string Nome { get; set; }
    public required string Slug { get; set; }

    /// <summary>SKU do modelo. O SKU vendavel fica na variacao.</summary>
    public required string SkuBase { get; set; }

    public string? Descricao { get; set; }

    public int IdCategoria { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public GeneroProduto Genero { get; set; } = GeneroProduto.Feminino;

    /// <summary>Preco em centavos.</summary>
    public int PrecoBaseCentavos { get; set; }

    /// <summary>O "de R$ X" riscado. E merchandising, nao promocao com vigencia — promocao real e cupom.</summary>
    public int? PrecoComparativoCentavos { get; set; }

    public string? ComposicaoTecido { get; set; }
    public string? InstrucoesLavagem { get; set; }
    public ModelagemProduto? Modelagem { get; set; }

    public int? IdTabelaMedidas { get; set; }
    public TabelaMedidas? TabelaMedidas { get; set; }

    public bool Destaque { get; set; }

    /// <summary>Soft delete. Produto nunca e apagado: o historico de pedidos depende dele.</summary>
    public bool Ativo { get; set; } = true;

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    /// <summary>Denormalizado: a listagem exibe estrelas em 40 cards por pagina.</summary>
    public decimal? NotaMedia { get; set; }
    public int TotalAvaliacoes { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<ProdutoVariacao> Variacoes { get; set; } = [];
    public ICollection<MidiaProduto> Midias { get; set; } = [];
    public ICollection<ProdutoColecao> Colecoes { get; set; } = [];
    public ICollection<Avaliacao> Avaliacoes { get; set; } = [];
    public ICollection<LogProduto> Logs { get; set; } = [];
}
