using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

/// <summary>Uma linha do guia de medidas: as medidas do corpo para um tamanho da grade.</summary>
public sealed record TabelaMedidasLinhaDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe o tamanho da linha.")]
    public int IdTamanho { get; init; }

    [Range(0.0, 500.0, ErrorMessage = "Medida invalida.")]
    public decimal? BustoCm { get; init; }

    [Range(0.0, 500.0, ErrorMessage = "Medida invalida.")]
    public decimal? CinturaCm { get; init; }

    [Range(0.0, 500.0, ErrorMessage = "Medida invalida.")]
    public decimal? QuadrilCm { get; init; }

    [Range(0.0, 500.0, ErrorMessage = "Medida invalida.")]
    public decimal? ComprimentoCm { get; init; }

    [Range(0.0, 500.0, ErrorMessage = "Medida invalida.")]
    public decimal? MangaCm { get; init; }

    public int Ordem { get; init; }
}

public sealed record TabelaMedidasLinhaResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdTamanho { get; init; }

    public string CodigoTamanho { get; init; } = string.Empty;

    public decimal? BustoCm { get; init; }

    public decimal? CinturaCm { get; init; }

    public decimal? QuadrilCm { get; init; }

    public decimal? ComprimentoCm { get; init; }

    public decimal? MangaCm { get; init; }

    public int Ordem { get; init; }
}

/// <summary>
/// A tabela e criada JUNTO com as linhas: um guia de medidas sem linha nao serve para nada e
/// deixar salvar vazio so cria registro morto que o admin nunca volta para completar.
/// </summary>
public sealed record TabelaMedidasCreateDto : CreateDto
{
    [Required(ErrorMessage = "O nome da tabela e obrigatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    public string? Observacao { get; init; }

    public bool Ativo { get; init; } = true;

    public IReadOnlyList<TabelaMedidasLinhaDto> Linhas { get; init; } = [];
}

/// <summary>
/// As linhas enviadas SUBSTITUEM as atuais. Diferenca item a item exigiria id de linha vindo do
/// navegador, e uma tabela de medidas tem seis linhas — reescrever o bloco e mais simples e nao
/// deixa linha orfa quando o admin remove um tamanho da grade.
/// </summary>
public sealed record TabelaMedidasUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O nome da tabela e obrigatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    public string? Observacao { get; init; }

    public bool Ativo { get; init; } = true;

    public IReadOnlyList<TabelaMedidasLinhaDto> Linhas { get; init; } = [];
}

public sealed record TabelaMedidasResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string? Observacao { get; init; }

    public bool Ativo { get; init; }

    public IReadOnlyList<TabelaMedidasLinhaResponseDto> Linhas { get; init; } = [];
}

// ======================================================================
// Guia de medidas PUBLICO (/api/v1/tabelas-medidas)
// ======================================================================

/// <summary>
/// Uma linha do guia de medidas como a LOJA a exibe.
///
/// DTO proprio, e nao o do painel, de proposito: o publico nao recebe o Id da linha (chave
/// interna que so serve para a edicao) nem qualquer campo administrativo. Endpoint publico que
/// reaproveita o DTO do admin passa a vazar campo novo automaticamente no dia em que alguem
/// acrescentar um ao painel — e ninguem percebe, porque nada quebra.
/// </summary>
public sealed record TabelaMedidasLinhaPublicaDto : ResponseDto
{
    public int IdTamanho { get; init; }

    /// <summary>Codigo exibido na primeira coluna do guia: P, M, G, 38, 40.</summary>
    public string CodigoTamanho { get; init; } = string.Empty;

    /// <summary>
    /// Posicao da linha na grade — e por ela que a lista ja vem ordenada.
    /// Ordenar pelo codigo colocaria "GG" antes de "P".
    /// </summary>
    public int OrdemTamanho { get; init; }

    public decimal? BustoCm { get; init; }

    public decimal? CinturaCm { get; init; }

    public decimal? QuadrilCm { get; init; }

    public decimal? ComprimentoCm { get; init; }

    public decimal? MangaCm { get; init; }
}

/// <summary>
/// Guia de medidas como a pagina /guia-de-medidas o consome: sem login e sem o campo Ativo —
/// o publico so enxerga tabela ativa, entao devolver a flag so daria ao front uma decisao que
/// ele nao precisa tomar (e que ele poderia tomar errado).
/// </summary>
public sealed record TabelaMedidasPublicaDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string? Observacao { get; init; }

    public IReadOnlyList<TabelaMedidasLinhaPublicaDto> Linhas { get; init; } = [];
}
