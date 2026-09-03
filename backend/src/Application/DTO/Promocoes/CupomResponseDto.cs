using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Cupom como sai no painel.
///
/// Nao existe campo "Vigente" aqui: decidir isso exige o relogio (IClock) e o mapeamento roda
/// sem acesso a servico. As datas cruas vao inteiras e quem exibe decide. "Esgotado", ao
/// contrario, e comparacao entre dois campos do proprio registro e pode viajar pronto.
/// </summary>
public sealed record CupomResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public TipoCupom Tipo { get; init; }

    /// <summary>Percentual x100 ou centavos, conforme o Tipo.</summary>
    public int Valor { get; init; }

    public int? ValorMinimoPedidoCentavos { get; init; }

    public int? DescontoMaximoCentavos { get; init; }

    public int? UsoMaximoTotal { get; init; }

    public int UsoMaximoPorUsuario { get; init; }

    public int UsosAtuais { get; init; }

    public DateTime VigenciaInicio { get; init; }

    public DateTime? VigenciaFim { get; init; }

    public bool PrimeiraCompraApenas { get; init; }

    public int? IdCategoriaRestrita { get; init; }

    public int? IdColecaoRestrita { get; init; }

    public bool Ativo { get; init; }

    /// <summary>Teto total ja consumido. Cupom sem teto nunca esgota.</summary>
    public bool Esgotado { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? DataAlteracao { get; init; }
}
