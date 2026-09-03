using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Social;

/// <summary>
/// Motivo da rejeicao. Obrigatorio: rejeicao sem motivo registrado transforma moderacao em
/// arbitrio e impede responder ao cliente que perguntar por que a review sumiu.
/// </summary>
public sealed record AvaliacaoRejeicaoDto
{
    [Required(ErrorMessage = "Informe o motivo da rejeicao.")]
    [StringLength(400, MinimumLength = 3, ErrorMessage = "O motivo deve ter de 3 a 400 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}
