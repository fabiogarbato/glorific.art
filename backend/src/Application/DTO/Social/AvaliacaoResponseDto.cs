using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Social;

/// <summary>
/// Avaliacao como sai na vitrine.
///
/// Autor e o PRIMEIRO NOME mais a inicial do sobrenome. Nome completo em pagina publica de
/// produto e exposicao gratuita de cliente, e e-mail nunca sai daqui.
///
/// Os campos de caimento vao expostos de proposito: e o dado que faz a pessoa escolher o tamanho
/// certo e nao devolver a peca.
/// </summary>
public sealed record AvaliacaoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public int Nota { get; init; }

    public string? Titulo { get; init; }

    public string? Comentario { get; init; }

    /// <summary>Nome abreviado do autor. Nunca o e-mail nem o nome completo.</summary>
    public string Autor { get; init; } = string.Empty;

    /// <summary>Selo "compra verificada": a avaliacao esta amarrada a um item de pedido do autor.</summary>
    public bool CompraVerificada { get; init; }

    public string? TamanhoComprado { get; init; }

    public int? AlturaClienteCm { get; init; }

    public decimal? PesoClienteKg { get; init; }

    public CaimentoTamanho? Caimento { get; init; }

    public bool? Recomenda { get; init; }

    public StatusAvaliacao Status { get; init; }

    public DateTime DataCriacao { get; init; }

    public IReadOnlyList<AvaliacaoMidiaResponseDto> Midias { get; init; } = [];
}
