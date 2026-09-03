namespace Glorific.Application.DTO.Social;

/// <summary>Foto de review ja resolvida em URL. O front nunca ve o id da midia bruta.</summary>
public sealed record AvaliacaoMidiaResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public int Ordem { get; init; }
}
