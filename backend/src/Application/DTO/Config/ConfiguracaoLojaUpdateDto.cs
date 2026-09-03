using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Config;

/// <summary>
/// Alteracao da configuracao da loja. Nao ha Id: a tabela e de linha unica.
///
/// Os Range existem porque erro de digitacao aqui e caro e silencioso: PrazoManuseioDias em 30
/// empurra o prazo de toda cotacao de frete da loja, e o sintoma aparece como "o site diz que
/// demora um mes" tres dias depois.
/// </summary>
public sealed record ConfiguracaoLojaUpdateDto : UpdateDto
{
    [Range(0, int.MaxValue, ErrorMessage = "Valor de frete gratis invalido.")]
    public int? FreteGratisAcimaDeCentavos { get; init; }

    [Range(0, 60, ErrorMessage = "O prazo de manuseio deve ficar entre 0 e 60 dias.")]
    public int PrazoManuseioDias { get; init; } = 2;

    [Required(ErrorMessage = "Informe o CEP de origem.")]
    [StringLength(9, MinimumLength = 8, ErrorMessage = "CEP invalido.")]
    public string CepOrigem { get; init; } = string.Empty;

    [Range(0, 365, ErrorMessage = "A politica de troca deve ficar entre 0 e 365 dias.")]
    public int PoliticaTrocaDias { get; init; } = 7;

    [Range(0, int.MaxValue, ErrorMessage = "Valor de pedido minimo invalido.")]
    public int? PedidoMinimoCentavos { get; init; }

    public bool ExibirEstoqueBaixo { get; init; }

    [Range(1, 999, ErrorMessage = "O limite de estoque baixo deve ficar entre 1 e 999.")]
    public int LimiteEstoqueBaixo { get; init; } = 3;
}
