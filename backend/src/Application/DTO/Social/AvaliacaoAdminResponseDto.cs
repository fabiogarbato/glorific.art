using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Social;

/// <summary>
/// Avaliacao na fila de moderacao.
///
/// Difere do DTO publico em duas coisas, e as duas sao intencionais: o moderador VE o e-mail e o
/// nome completo de quem escreveu (e ele quem decide se o texto e legitimo) e ve o motivo de
/// rejeicao e quem moderou. Nada disso pode vazar para a vitrine, por isso sao dois DTOs e nao um
/// com campos condicionais.
/// </summary>
public sealed record AvaliacaoAdminResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public string NomeProduto { get; init; } = string.Empty;

    public int IdUsuario { get; init; }

    public string? NomeUsuario { get; init; }

    public string? EmailUsuario { get; init; }

    public bool CompraVerificada { get; init; }

    public int Nota { get; init; }

    public string? Titulo { get; init; }

    public string? Comentario { get; init; }

    public string? TamanhoComprado { get; init; }

    public int? AlturaClienteCm { get; init; }

    public decimal? PesoClienteKg { get; init; }

    public CaimentoTamanho? Caimento { get; init; }

    public bool? Recomenda { get; init; }

    public StatusAvaliacao Status { get; init; }

    public string? MotivoRejeicao { get; init; }

    public int? ModeradaPor { get; init; }

    public DateTime? ModeradaEm { get; init; }

    public DateTime DataCriacao { get; init; }

    public IReadOnlyList<AvaliacaoMidiaResponseDto> Midias { get; init; } = [];
}
