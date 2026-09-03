namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Uma linha do ledger cupons_usos, do jeito que o painel precisa ler: quem usou, em que pedido
/// e quanto de fato foi descontado.
///
/// ValorDescontadoCentavos vem gravado do dia do uso, e nao recalculado: as regras (teto,
/// restricao de categoria) podem ter mudado depois, e o relatorio de investimento em promocao
/// precisa do numero que realmente saiu.
/// </summary>
public sealed record CupomUsoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdCupom { get; init; }

    public int IdUsuario { get; init; }

    public string? EmailUsuario { get; init; }

    public string? NomeUsuario { get; init; }

    public int IdPedido { get; init; }

    public string? NumeroPedido { get; init; }

    public int ValorDescontadoCentavos { get; init; }

    public DateTime DataUso { get; init; }
}
