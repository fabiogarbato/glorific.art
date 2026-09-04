namespace Glorific.Application.DTO.Catalogo;

/// <summary>Resposta da geração de descrição por IA. Não persiste nada — é sugestão pro admin revisar.</summary>
public sealed record DescricaoGeradaDto
{
    public required string Descricao { get; init; }
}
