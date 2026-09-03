using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Catalogo;

public sealed record ProdutoCreateDto : CreateDto
{
    [Required(ErrorMessage = "O nome do produto e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Slug { get; init; }

    /// <summary>SKU do MODELO. O SKU vendavel fica na variacao.</summary>
    [Required(ErrorMessage = "O SKU base e obrigatorio.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O SKU base deve ter entre 2 e 60 caracteres.")]
    public string SkuBase { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Informe a categoria.")]
    public int IdCategoria { get; init; }

    public GeneroProduto Genero { get; init; } = GeneroProduto.Feminino;

    [Range(0, int.MaxValue, ErrorMessage = "O preco base em centavos nao pode ser negativo.")]
    public int PrecoBaseCentavos { get; init; }

    /// <summary>O "de R$ X" riscado. Merchandising, nao promocao com vigencia.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "O preco comparativo em centavos nao pode ser negativo.")]
    public int? PrecoComparativoCentavos { get; init; }

    [StringLength(400)]
    public string? ComposicaoTecido { get; init; }

    public string? InstrucoesLavagem { get; init; }

    public ModelagemProduto? Modelagem { get; init; }

    public int? IdTabelaMedidas { get; init; }

    public bool Destaque { get; init; }

    [StringLength(200)]
    public string? MetaTitle { get; init; }

    [StringLength(400)]
    public string? MetaDescription { get; init; }

    /// <summary>Colecoes ("Capsula Advento") em que a peca entra. Curadoria, nao taxonomia.</summary>
    public IReadOnlyList<int> IdsColecoes { get; init; } = [];
}

public sealed record ProdutoUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O nome do produto e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Vazio mantem o slug atual: trocar slug quebra link ja indexado.</summary>
    [StringLength(200)]
    public string? Slug { get; init; }

    [Required(ErrorMessage = "O SKU base e obrigatorio.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O SKU base deve ter entre 2 e 60 caracteres.")]
    public string SkuBase { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Informe a categoria.")]
    public int IdCategoria { get; init; }

    public GeneroProduto Genero { get; init; } = GeneroProduto.Feminino;

    [Range(0, int.MaxValue, ErrorMessage = "O preco base em centavos nao pode ser negativo.")]
    public int PrecoBaseCentavos { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "O preco comparativo em centavos nao pode ser negativo.")]
    public int? PrecoComparativoCentavos { get; init; }

    [StringLength(400)]
    public string? ComposicaoTecido { get; init; }

    public string? InstrucoesLavagem { get; init; }

    public ModelagemProduto? Modelagem { get; init; }

    public int? IdTabelaMedidas { get; init; }

    public bool Destaque { get; init; }

    [StringLength(200)]
    public string? MetaTitle { get; init; }

    [StringLength(400)]
    public string? MetaDescription { get; init; }

    /// <summary>
    /// Null NAO mexe nos vinculos de colecao; lista vazia REMOVE todos. Sao intencoes
    /// diferentes, e tratar as duas como "vazio" faria toda edicao de preco esvaziar a curadoria.
    /// </summary>
    public IReadOnlyList<int>? IdsColecoes { get; init; }
}

/// <summary>Visao administrativa completa da peca: e o que a tela de edicao carrega.</summary>
public sealed record ProdutoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string SkuBase { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public int IdCategoria { get; init; }

    public string? NomeCategoria { get; init; }

    public string? SlugCategoria { get; init; }

    public GeneroProduto Genero { get; init; }

    public int PrecoBaseCentavos { get; init; }

    public int? PrecoComparativoCentavos { get; init; }

    public string? ComposicaoTecido { get; init; }

    public string? InstrucoesLavagem { get; init; }

    public ModelagemProduto? Modelagem { get; init; }

    public int? IdTabelaMedidas { get; init; }

    public bool Destaque { get; init; }

    public bool Ativo { get; init; }

    public string? MetaTitle { get; init; }

    public string? MetaDescription { get; init; }

    public decimal? NotaMedia { get; init; }

    public int TotalAvaliacoes { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? DataAlteracao { get; init; }

    /// <summary>Soma do disponivel de todos os SKUs — o numero que o admin olha primeiro.</summary>
    public int EstoqueTotalDisponivel { get; init; }

    public int TotalVariacoes { get; init; }

    public IReadOnlyList<ProdutoVariacaoResponseDto> Variacoes { get; init; } = [];

    public IReadOnlyList<MidiaProdutoResponseDto> Midias { get; init; } = [];

    public IReadOnlyList<ColecaoResponseDto> Colecoes { get; init; } = [];
}

/// <summary>Auditoria de ativacao/desativacao — responde "quem tirou isso do ar e quando".</summary>
public sealed record ProdutoLogResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public bool? AtivoAntigo { get; init; }

    public bool AtivoNovo { get; init; }

    public int? IdUsuario { get; init; }

    public string? NomeUsuario { get; init; }

    public DateTime DataAlteracao { get; init; }
}
