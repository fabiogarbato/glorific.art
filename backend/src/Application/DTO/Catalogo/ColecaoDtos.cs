using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

public sealed record ColecaoCreateDto : CreateDto
{
    [Required(ErrorMessage = "O nome da colecao e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Slug { get; init; }

    public string? Descricao { get; init; }

    /// <summary>Versiculo ou frase que abre a colecao na vitrine.</summary>
    [StringLength(400)]
    public string? Epigrafe { get; init; }

    public int? IdMidiaCapa { get; init; }

    public int? IdMidiaBanner { get; init; }

    /// <summary>Inicio da vigencia em UTC. Null = ja vale.</summary>
    public DateTime? DataInicio { get; init; }

    /// <summary>Fim da vigencia em UTC. Null = sem prazo.</summary>
    public DateTime? DataFim { get; init; }

    public bool Destaque { get; init; }

    public bool Habilitado { get; init; } = true;

    public int Ordem { get; init; }
}

public sealed record ColecaoUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O nome da colecao e obrigatorio.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 180 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Slug { get; init; }

    public string? Descricao { get; init; }

    [StringLength(400)]
    public string? Epigrafe { get; init; }

    public int? IdMidiaCapa { get; init; }

    public int? IdMidiaBanner { get; init; }

    public DateTime? DataInicio { get; init; }

    public DateTime? DataFim { get; init; }

    public bool Destaque { get; init; }

    public bool Habilitado { get; init; } = true;

    public int Ordem { get; init; }
}

public sealed record ColecaoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public string? Epigrafe { get; init; }

    public int? IdMidiaCapa { get; init; }

    public string? UrlMidiaCapa { get; init; }

    public int? IdMidiaBanner { get; init; }

    public string? UrlMidiaBanner { get; init; }

    public DateTime? DataInicio { get; init; }

    public DateTime? DataFim { get; init; }

    public bool Destaque { get; init; }

    public bool Habilitado { get; init; }

    public int Ordem { get; init; }
}

/// <summary>Vinculo de produto a colecao. A ordem e curadoria manual da vitrine do drop.</summary>
public sealed record VincularProdutoColecaoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe o produto.")]
    public int IdProduto { get; init; }

    public int Ordem { get; init; }
}
