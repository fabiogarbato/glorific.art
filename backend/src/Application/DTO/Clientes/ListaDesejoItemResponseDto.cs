namespace Glorific.Application.DTO.Clientes;

/// <summary>
/// Item da lista de desejos como card de vitrine.
///
/// ProdutoAtivo vem exposto porque o repositorio le a lista ignorando o filtro de soft delete: a
/// peca que saiu do catalogo continua aparecendo marcada como indisponivel, que e exatamente o
/// item sobre o qual o cliente quer ser avisado quando voltar. Sumir com a linha calada seria o
/// pior dos dois mundos.
/// </summary>
public sealed record ListaDesejoItemResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdProduto { get; init; }

    public string NomeProduto { get; init; } = string.Empty;

    public string SlugProduto { get; init; } = string.Empty;

    public int PrecoCentavos { get; init; }

    public int? PrecoComparativoCentavos { get; init; }

    public string? ImagemUrl { get; init; }

    public bool ProdutoAtivo { get; init; }

    public int? IdVariacao { get; init; }

    public string? TamanhoVariacao { get; init; }

    public string? CorVariacao { get; init; }

    /// <summary>Disponibilidade da VARIACAO escolhida. Null quando o cliente favoritou so a peca.</summary>
    public bool? VariacaoDisponivel { get; init; }

    public DateTime DataCriacao { get; init; }
}
