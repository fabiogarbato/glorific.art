using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Catalogo;

public sealed record CorCreateDto : CreateDto
{
    [Required(ErrorMessage = "O nome da cor e obrigatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Slug { get; init; }

    /// <summary>
    /// Swatch solido no formato #RRGGBB. O regex barra "#fff" e "terracota" antes do banco,
    /// porque o front pinta a bolinha direto com este valor e um hex invalido some da tela.
    /// </summary>
    [Required(ErrorMessage = "A cor hexadecimal e obrigatoria.")]
    [RegularExpression("^#(?:[0-9a-fA-F]{6})$", ErrorMessage = "Informe a cor no formato #RRGGBB.")]
    public string HexRgb { get; init; } = string.Empty;

    /// <summary>Estampa (xadrez, floral): a cor chapada nao representa a peca.</summary>
    public int? IdMidiaSwatch { get; init; }

    public int Ordem { get; init; }

    public bool Ativo { get; init; } = true;
}

public sealed record CorUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O nome da cor e obrigatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Slug { get; init; }

    [Required(ErrorMessage = "A cor hexadecimal e obrigatoria.")]
    [RegularExpression("^#(?:[0-9a-fA-F]{6})$", ErrorMessage = "Informe a cor no formato #RRGGBB.")]
    public string HexRgb { get; init; } = string.Empty;

    public int? IdMidiaSwatch { get; init; }

    public int Ordem { get; init; }

    public bool Ativo { get; init; } = true;
}

public sealed record CorResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string HexRgb { get; init; } = string.Empty;

    public int? IdMidiaSwatch { get; init; }

    public string? UrlMidiaSwatch { get; init; }

    public int Ordem { get; init; }

    public bool Ativo { get; init; }
}
