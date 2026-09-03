using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

/// <summary>
/// Criacao de categoria. O Slug e OPCIONAL de proposito: o admin de moda cadastra dezenas de
/// categorias e derivar do nome evita erro de digitacao numa URL que e SEO-critica. Quando
/// informado, ele vence — mas ainda passa pela normalizacao e pela desambiguacao.
/// </summary>
public sealed record CategoriaCreateDto : CreateDto
{
    [Required(ErrorMessage = "O nome da categoria e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(200, ErrorMessage = "O slug deve ter no maximo 200 caracteres.")]
    public string? Slug { get; init; }

    public string? Descricao { get; init; }

    /// <summary>Pai da auto-relacao de UM nivel ("Vestidos" &gt; "Midi").</summary>
    public int? IdCategoriaPai { get; init; }

    public int? IdMidiaCapa { get; init; }

    public int Ordem { get; init; }

    public bool Habilitado { get; init; } = true;

    [StringLength(200)]
    public string? MetaTitle { get; init; }

    [StringLength(400)]
    public string? MetaDescription { get; init; }
}

public sealed record CategoriaUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O nome da categoria e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>
    /// Vazio mantem o slug atual. Trocar slug quebra link indexado, entao a mudanca e sempre
    /// deliberada — nunca um efeito colateral de renomear a categoria.
    /// </summary>
    [StringLength(200)]
    public string? Slug { get; init; }

    public string? Descricao { get; init; }

    public int? IdCategoriaPai { get; init; }

    public int? IdMidiaCapa { get; init; }

    public int Ordem { get; init; }

    public bool Habilitado { get; init; } = true;

    [StringLength(200)]
    public string? MetaTitle { get; init; }

    [StringLength(400)]
    public string? MetaDescription { get; init; }
}

public sealed record CategoriaResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public int? IdCategoriaPai { get; init; }

    public int? IdMidiaCapa { get; init; }

    /// <summary>Achatado da midia: o front nao precisa de uma segunda chamada para exibir a capa.</summary>
    public string? UrlMidiaCapa { get; init; }

    public int Ordem { get; init; }

    public bool Habilitado { get; init; }

    public string? MetaTitle { get; init; }

    public string? MetaDescription { get; init; }

    /// <summary>Preenchido apenas na arvore do menu; vazio na listagem paginada.</summary>
    public IReadOnlyList<CategoriaResponseDto> Filhas { get; init; } = [];
}
