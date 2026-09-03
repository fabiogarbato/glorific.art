using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

/// <summary>
/// Criacao do SKU vendavel.
///
/// Peso e dimensoes sao obrigatorios e POSITIVOS aqui, nao so quando a variacao e publicada:
/// o banco tem CHECK (peso_gramas &gt; 0 AND altura_cm &gt; 0 ...) e um zero passaria pela
/// validacao de tela para estourar como erro cru de constraint. A regra de PUBLICACAO (§2 da
/// vertical) e verificada de novo no servico, com mensagem propria.
/// </summary>
public sealed record ProdutoVariacaoCreateDto : CreateDto
{
    /// <summary>Vem da ROTA (/admin/produtos/{id}/variacoes). O controller preenche.</summary>
    public int IdProduto { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Informe o tamanho.")]
    public int IdTamanho { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Informe a cor.")]
    public int IdCor { get; init; }

    /// <summary>Vazio = gerado a partir do SKU base do produto + tamanho + cor.</summary>
    [StringLength(60)]
    public string? Sku { get; init; }

    /// <summary>Override em centavos. Null herda o preco base do produto.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "O preco em centavos nao pode ser negativo.")]
    public int? PrecoCentavos { get; init; }

    [StringLength(20)]
    public string? CodigoBarras { get; init; }

    [Range(1, 100_000, ErrorMessage = "O peso em gramas deve ser maior que zero.")]
    public int PesoGramas { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A altura em cm deve ser maior que zero.")]
    public decimal AlturaCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A largura em cm deve ser maior que zero.")]
    public decimal LarguraCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "O comprimento em cm deve ser maior que zero.")]
    public decimal ComprimentoCm { get; init; }

    public bool Ativo { get; init; } = true;

    /// <summary>Saldo inicial da prateleira. A linha de estoque nasce junto com o SKU.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "A quantidade inicial nao pode ser negativa.")]
    public int QuantidadeInicial { get; init; }

    /// <summary>Limite do alerta de estoque baixo no painel.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "A quantidade minima nao pode ser negativa.")]
    public int QuantidadeMinima { get; init; }
}

public sealed record ProdutoVariacaoUpdateDto : UpdateDto
{
    /// <summary>Vazio mantem o SKU atual — ele aparece em pedido e etiqueta ja emitidos.</summary>
    [StringLength(60)]
    public string? Sku { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "O preco em centavos nao pode ser negativo.")]
    public int? PrecoCentavos { get; init; }

    [StringLength(20)]
    public string? CodigoBarras { get; init; }

    [Range(1, 100_000, ErrorMessage = "O peso em gramas deve ser maior que zero.")]
    public int PesoGramas { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A altura em cm deve ser maior que zero.")]
    public decimal AlturaCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A largura em cm deve ser maior que zero.")]
    public decimal LarguraCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "O comprimento em cm deve ser maior que zero.")]
    public decimal ComprimentoCm { get; init; }

    public bool Ativo { get; init; } = true;
}

public sealed record ProdutoVariacaoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public string Sku { get; init; } = string.Empty;

    public int IdTamanho { get; init; }

    public string CodigoTamanho { get; init; } = string.Empty;

    public int OrdemTamanho { get; init; }

    public int IdCor { get; init; }

    public string NomeCor { get; init; } = string.Empty;

    public string SlugCor { get; init; } = string.Empty;

    public string HexRgb { get; init; } = string.Empty;

    public int? PrecoCentavos { get; init; }

    /// <summary>Preco que vale de fato: override da variacao ou preco base do produto.</summary>
    public int PrecoEfetivoCentavos { get; init; }

    public string? CodigoBarras { get; init; }

    public int PesoGramas { get; init; }

    public decimal AlturaCm { get; init; }

    public decimal LarguraCm { get; init; }

    public decimal ComprimentoCm { get; init; }

    public bool Ativo { get; init; }

    public int QuantidadeEmEstoque { get; init; }

    public int QuantidadeReservada { get; init; }

    /// <summary>Quantidade - reservada. E o que pode ser vendido agora.</summary>
    public int QuantidadeDisponivel { get; init; }

    public int QuantidadeMinima { get; init; }
}

/// <summary>
/// Geracao de grade em LOTE: a matriz tamanhos x cores de uma vez.
///
/// E o que torna o cadastro de moda viavel — uma peca em 5 tamanhos e 4 cores sao 20 SKUs, e
/// cadastrar um a um faz o admin desistir e vender tudo como tamanho unico.
/// As combinacoes que ja existem sao IGNORADAS, nunca sobrescritas: reexecutar completa a grade
/// depois de acrescentar uma cor, sem apagar preco ou peso ja ajustados a mao.
/// </summary>
public sealed record GerarGradeDto
{
    [Required(ErrorMessage = "Informe ao menos um tamanho.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um tamanho.")]
    public IReadOnlyList<int> IdsTamanhos { get; init; } = [];

    [Required(ErrorMessage = "Informe ao menos uma cor.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma cor.")]
    public IReadOnlyList<int> IdsCores { get; init; } = [];

    [Range(1, 100_000, ErrorMessage = "O peso em gramas deve ser maior que zero.")]
    public int PesoGramas { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A altura em cm deve ser maior que zero.")]
    public decimal AlturaCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "A largura em cm deve ser maior que zero.")]
    public decimal LarguraCm { get; init; }

    [Range(0.01, 999_999.99, ErrorMessage = "O comprimento em cm deve ser maior que zero.")]
    public decimal ComprimentoCm { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "O preco em centavos nao pode ser negativo.")]
    public int? PrecoCentavos { get; init; }

    /// <summary>Vazio usa o SKU base do produto.</summary>
    [StringLength(30)]
    public string? PrefixoSku { get; init; }

    public bool Ativo { get; init; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "A quantidade inicial nao pode ser negativa.")]
    public int QuantidadeInicial { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "A quantidade minima nao pode ser negativa.")]
    public int QuantidadeMinima { get; init; }
}

public sealed record GradeGeradaDto : ResponseDto
{
    public int IdProduto { get; init; }

    public int Criadas { get; init; }

    /// <summary>Combinacoes que ja existiam e foram preservadas como estavam.</summary>
    public int JaExistiam { get; init; }

    /// <summary>A grade completa do produto depois da operacao.</summary>
    public IReadOnlyList<ProdutoVariacaoResponseDto> Variacoes { get; init; } = [];
}
