using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

/// <summary>
/// Registro de midia JA hospedada (import de acervo, migracao). O caminho normal e o upload,
/// que passa pelo IImageStorage e devolve o mesmo <see cref="MidiaResponseDto"/>.
/// </summary>
public sealed record MidiaCreateDto : CreateDto
{
    [Required(ErrorMessage = "A URL da imagem e obrigatoria.")]
    [StringLength(500)]
    public string Url { get; init; } = string.Empty;

    /// <summary>Identificador no provedor de storage, necessario para remover o arquivo depois.</summary>
    [StringLength(300)]
    public string? PublicId { get; init; }

    [StringLength(300)]
    public string? AltText { get; init; }

    public int? Largura { get; init; }

    public int? Altura { get; init; }

    public long? TamanhoBytes { get; init; }

    [StringLength(120)]
    public string? ContentType { get; init; }
}

/// <summary>
/// So o texto alternativo e editavel. Trocar a URL de uma midia ja vinculada mudaria a foto de
/// todo produto que a referencia sem deixar rastro — quem quer outra imagem sobe outra imagem.
/// </summary>
public sealed record MidiaUpdateDto : UpdateDto
{
    [StringLength(300)]
    public string? AltText { get; init; }
}

public sealed record MidiaResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? PublicId { get; init; }

    public string? AltText { get; init; }

    public int? Largura { get; init; }

    public int? Altura { get; init; }

    public long? TamanhoBytes { get; init; }

    public string? ContentType { get; init; }

    public DateTime DataCriacao { get; init; }
}

/// <summary>Vincula uma midia ja existente a galeria de um produto.</summary>
public sealed record VincularMidiaProdutoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe a midia.")]
    public int IdMidia { get; init; }

    /// <summary>
    /// Null = foto neutra, aparece em qualquer swatch. Preenchido = galeria POR COR: clicar em
    /// "Terracota" troca as fotos, comportamento esperado em moda.
    /// </summary>
    public int? IdCor { get; init; }

    public int Ordem { get; init; }

    public bool EhCapa { get; init; }
}

/// <summary>Nova ordem da galeria. A primeira posicao vira a capa, por ordem EXPLICITA.</summary>
public sealed record ReordenarGaleriaDto
{
    [Required(ErrorMessage = "Informe a nova ordem da galeria.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public IReadOnlyList<int> IdsNaOrdem { get; init; } = [];
}

public sealed record MidiaProdutoResponseDto : ResponseDto
{
    /// <summary>Id da LINHA da galeria (midias_produtos), nao da midia. E ele que reordena.</summary>
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public int IdMidia { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public int? Largura { get; init; }

    public int? Altura { get; init; }

    public int? IdCor { get; init; }

    public string? NomeCor { get; init; }

    public string? SlugCor { get; init; }

    public int Ordem { get; init; }

    public bool EhCapa { get; init; }
}
