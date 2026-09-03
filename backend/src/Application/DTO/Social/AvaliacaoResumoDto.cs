using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Social;

/// <summary>
/// Resumo das avaliacoes APROVADAS de um produto, agregado no banco.
///
/// CaimentoPredominante e o campo que importa em moda: e ele que vira "a maioria diz que veste
/// pequeno — considere um numero acima" na pagina do produto. Sem isso a avaliacao vira enfeite
/// de estrela e nao reduz devolucao nenhuma.
/// </summary>
public sealed record AvaliacaoResumoDto : ResponseDto
{
    public int IdProduto { get; init; }

    /// <summary>Null quando nao ha avaliacao aprovada. Zero estrela e uma nota; ausencia e outra coisa.</summary>
    public decimal? NotaMedia { get; init; }

    public int TotalAvaliacoes { get; init; }

    /// <summary>Nota (1 a 5) -> quantidade. Chaves sem avaliacao vem com zero.</summary>
    public IReadOnlyDictionary<int, int> DistribuicaoPorNota { get; init; } =
        new Dictionary<int, int>();

    /// <summary>Percentual inteiro de quem recomendou, entre quem respondeu. Null quando ninguem respondeu.</summary>
    public int? PercentualRecomenda { get; init; }

    /// <summary>Caimento mais votado. Null quando ninguem informou.</summary>
    public CaimentoTamanho? CaimentoPredominante { get; init; }

    /// <summary>Quantos responderam o caimento — o peso da recomendacao de tamanho.</summary>
    public int TotalRespostasCaimento { get; init; }
}
