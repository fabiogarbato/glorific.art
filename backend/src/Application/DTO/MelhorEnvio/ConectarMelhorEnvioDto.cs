namespace Glorific.Application.DTO.MelhorEnvio;

/// <summary>Corpo do POST que completa a troca do "code" do retorno OAuth por token.</summary>
public sealed record ConectarMelhorEnvioDto
{
    public required string Code { get; init; }

    public required string State { get; init; }
}
