using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Catalogo;

public sealed record TamanhoCreateDto : CreateDto
{
    [Required(ErrorMessage = "O codigo do tamanho e obrigatorio.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "O codigo deve ter entre 1 e 10 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [StringLength(120)]
    public string? Descricao { get; init; }

    /// <summary>
    /// Ordem de EXIBICAO no seletor. Sem ela "GG" viria antes de "P" na ordenacao alfabetica.
    /// </summary>
    public int Ordem { get; init; }

    public GradeTamanho Grade { get; init; } = GradeTamanho.Alfa;

    public bool Ativo { get; init; } = true;
}

public sealed record TamanhoUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "O codigo do tamanho e obrigatorio.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "O codigo deve ter entre 1 e 10 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [StringLength(120)]
    public string? Descricao { get; init; }

    public int Ordem { get; init; }

    public GradeTamanho Grade { get; init; } = GradeTamanho.Alfa;

    public bool Ativo { get; init; } = true;
}

public sealed record TamanhoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public int Ordem { get; init; }

    public GradeTamanho Grade { get; init; }

    public bool Ativo { get; init; }
}
