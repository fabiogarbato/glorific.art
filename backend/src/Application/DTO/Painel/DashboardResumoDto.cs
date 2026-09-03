namespace Glorific.Application.DTO.Painel;

/// <summary>
/// Painel inicial do admin.
///
/// Duas datas diferentes sustentam este resumo, e a distincao e proposital:
/// faturamento e ranking usam DATA DE PAGAMENTO (dinheiro entrou), enquanto "pedidos por status"
/// usa DATA DE CRIACAO (o que o operador tem para trabalhar). Misturar as duas produz o relatorio
/// classico em que a soma dos status nao bate com o numero de pedidos pagos e ninguem entende por que.
///
/// Os blocos operacionais (estoque, envio, moderacao) NAO sao filtrados por periodo: pendencia
/// nao expira porque o filtro do painel mudou.
/// </summary>
public sealed record DashboardResumoDto : ResponseDto
{
    public DateTime PeriodoInicio { get; init; }

    public DateTime PeriodoFim { get; init; }

    /// <summary>Soma de Pedido.TotalCentavos dos pedidos pagos no periodo.</summary>
    public int FaturamentoCentavos { get; init; }

    public int PedidosPagos { get; init; }

    /// <summary>Faturamento dividido por pedidos pagos, em centavos. Zero quando nao houve pedido.</summary>
    public int TicketMedioCentavos { get; init; }

    /// <summary>Frete cobrado do cliente no periodo. O custo real esta em Envio.ValorCompradoCentavos.</summary>
    public int FreteCobradoCentavos { get; init; }

    /// <summary>Quanto de cupom foi concedido no periodo — o investimento real em promocao.</summary>
    public int DescontoConcedidoCentavos { get; init; }

    /// <summary>Pedidos CRIADOS no periodo, agrupados por status.</summary>
    public IReadOnlyList<DashboardPedidoStatusDto> PedidosPorStatus { get; init; } = [];

    public IReadOnlyList<DashboardProdutoVendidoDto> ProdutosMaisVendidos { get; init; } = [];

    /// <summary>Contagem total de SKUs abaixo do minimo, independente do quanto a lista mostra.</summary>
    public int TotalEstoqueAbaixoDoMinimo { get; init; }

    public IReadOnlyList<DashboardEstoqueBaixoDto> EstoqueCritico { get; init; } = [];

    public int TotalEnviosComProblema { get; init; }

    public IReadOnlyList<DashboardEnvioProblemaDto> FilaEnvioComProblema { get; init; } = [];

    public int AvaliacoesPendentes { get; init; }
}
