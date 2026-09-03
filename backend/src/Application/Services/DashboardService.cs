using Glorific.Application.DTO.Painel;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;

namespace Glorific.Application.Services;

/// <summary>
/// Painel administrativo.
///
/// A regra que organiza o arquivo inteiro: nada e somado em memoria. Todo numero sai de um
/// GROUP BY no Postgres e volta ja agregado — no maximo algumas dezenas de linhas por consulta.
/// A tentacao de "carregar os pedidos do mes e somar em C#" funciona no primeiro ano e vira
/// timeout no segundo.
///
/// Duas datas diferentes sustentam o resumo, e a distincao e deliberada:
/// dinheiro e ranking usam DATA DE PAGAMENTO (foi quando entrou), enquanto "pedidos por status"
/// usa DATA DE CRIACAO (e o que o operador tem na mesa). Misturar as duas produz o relatorio em
/// que a soma dos status nao bate com a contagem de pedidos pagos e ninguem entende por que.
///
/// Os blocos operacionais (estoque, fila de envio, moderacao) nao respeitam o periodo: pendencia
/// nao deixa de existir porque o filtro do painel mudou.
/// </summary>
public class DashboardService : IDashboardService
{
    /// <summary>Faturamento so conta pedido com pagamento confirmado que nao foi desfeito.</summary>
    private static readonly StatusPedido[] StatusFaturados =
    [
        StatusPedido.Pago,
        StatusPedido.EmSeparacao,
        StatusPedido.Enviado,
        StatusPedido.Entregue
    ];

    /// <summary>Envio que ainda nao virou etiqueta: e onde o worker pode estar travado.</summary>
    private static readonly StatusEnvio[] StatusEnvioEmAndamento =
    [
        StatusEnvio.Pendente,
        StatusEnvio.NoCarrinho,
        StatusEnvio.Comprado
    ];

    private const int LimitePadraoLista = 10;
    private const int DiasPadraoPeriodo = 30;

    private readonly IPedidoRepository _pedidos;
    private readonly IEstoqueRepository _estoques;
    private readonly IEnvioRepository _envios;
    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IConsultaAssincrona _consulta;
    private readonly IClock _relogio;

    public DashboardService(
        IPedidoRepository pedidos,
        IEstoqueRepository estoques,
        IEnvioRepository envios,
        IAvaliacaoRepository avaliacoes,
        IConsultaAssincrona consulta,
        IClock relogio)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
        _envios = envios ?? throw new ArgumentNullException(nameof(envios));
        _avaliacoes = avaliacoes ?? throw new ArgumentNullException(nameof(avaliacoes));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <inheritdoc />
    public async Task<DashboardResumoDto> ObterResumoAsync(
        DateTime? de = null,
        DateTime? ate = null,
        CancellationToken cancellationToken = default)
    {
        var agora = _relogio.UtcNow;

        var inicio = de ?? agora.AddDays(-DiasPadraoPeriodo);
        var fim = ate ?? agora;

        // Inverter as datas na query string e erro comum de operador. Corrigir e silencioso e
        // devolve o que a pessoa queria; recusar so produziria um painel em branco sem explicacao.
        if (fim < inicio)
            (inicio, fim) = (fim, inicio);

        var faturados = _pedidos.Query()
            .Where(pedido => pedido.DataPagamento != null
                             && pedido.DataPagamento >= inicio
                             && pedido.DataPagamento <= fim
                             && StatusFaturados.Contains(pedido.Status));

        var criadosNoPeriodo = _pedidos.Query()
            .Where(pedido => pedido.DataCriacao >= inicio && pedido.DataCriacao <= fim);

        var financeiro = await ObterFinanceiroAsync(faturados, cancellationToken);
        var porStatus = await ObterPedidosPorStatusAsync(criadosNoPeriodo, cancellationToken);
        var maisVendidos = await ObterMaisVendidosAsync(faturados, cancellationToken);
        var (totalEstoqueBaixo, estoqueCritico) = await ObterEstoqueBaixoAsync(cancellationToken);
        var (totalEnviosProblema, filaEnvio) = await ObterEnviosComProblemaAsync(cancellationToken);

        var avaliacoesPendentes = await _consulta.ContarAsync(
            _avaliacoes.Query().Where(avaliacao => avaliacao.Status == StatusAvaliacao.Pendente),
            cancellationToken);

        return new DashboardResumoDto
        {
            PeriodoInicio = inicio,
            PeriodoFim = fim,
            FaturamentoCentavos = financeiro.Faturamento,
            PedidosPagos = financeiro.Pedidos,

            // Divisao inteira em centavos: ticket medio e indicador, nao lancamento contabil.
            TicketMedioCentavos = financeiro.Pedidos == 0 ? 0 : financeiro.Faturamento / financeiro.Pedidos,

            FreteCobradoCentavos = financeiro.Frete,
            DescontoConcedidoCentavos = financeiro.Desconto,
            PedidosPorStatus = porStatus,
            ProdutosMaisVendidos = maisVendidos,
            TotalEstoqueAbaixoDoMinimo = totalEstoqueBaixo,
            EstoqueCritico = estoqueCritico,
            TotalEnviosComProblema = totalEnviosProblema,
            FilaEnvioComProblema = filaEnvio,
            AvaliacoesPendentes = avaliacoesPendentes
        };
    }

    // ------------------------------------------------------------------
    // Blocos
    // ------------------------------------------------------------------

    /// <summary>
    /// Faturamento, contagem, frete cobrado e desconto concedido em UMA ida ao banco.
    ///
    /// O GroupBy por constante e o que transforma quatro agregacoes em um unico SELECT com quatro
    /// funcoes de agregacao. long nas somas porque a soma de centavos de um ano de loja passa de
    /// dois bilhoes com facilidade — int estouraria calado.
    /// </summary>
    private async Task<(int Faturamento, int Pedidos, int Frete, int Desconto)> ObterFinanceiroAsync(
        IQueryable<Pedido> faturados,
        CancellationToken cancellationToken)
    {
        var agregado = await _consulta.PrimeiroOuPadraoAsync(
            faturados
                .GroupBy(pedido => 1)
                .Select(grupo => new
                {
                    Faturamento = grupo.Sum(pedido => (long)pedido.TotalCentavos),
                    Pedidos = grupo.Count(),
                    Frete = grupo.Sum(pedido => (long)pedido.FreteCentavos),
                    Desconto = grupo.Sum(pedido => (long)pedido.DescontoCupomCentavos)
                }),
            cancellationToken);

        if (agregado is null)
            return (0, 0, 0, 0);

        return (
            EmInteiro(agregado.Faturamento),
            agregado.Pedidos,
            EmInteiro(agregado.Frete),
            EmInteiro(agregado.Desconto));
    }

    private async Task<IReadOnlyList<DashboardPedidoStatusDto>> ObterPedidosPorStatusAsync(
        IQueryable<Pedido> criadosNoPeriodo,
        CancellationToken cancellationToken)
    {
        // No maximo dez linhas voltam: uma por valor do enum StatusPedido.
        var linhas = await _consulta.ListarAsync(
            criadosNoPeriodo
                .GroupBy(pedido => pedido.Status)
                .Select(grupo => new
                {
                    Status = grupo.Key,
                    Quantidade = grupo.Count(),
                    Total = grupo.Sum(pedido => (long)pedido.TotalCentavos)
                }),
            cancellationToken);

        return
        [
            .. linhas
                .OrderByDescending(linha => linha.Quantidade)
                .Select(linha => new DashboardPedidoStatusDto
                {
                    Status = linha.Status,
                    StatusNome = linha.Status.ToString(),
                    Quantidade = linha.Quantidade,
                    TotalCentavos = EmInteiro(linha.Total)
                })
        ];
    }

    /// <summary>
    /// Ranking por SNAPSHOT do item, nunca pelo catalogo atual: renomear a peca no admin nao pode
    /// reescrever o relatorio do mes passado, e peca desativada tem de continuar aparecendo no
    /// periodo em que vendeu.
    /// </summary>
    private async Task<IReadOnlyList<DashboardProdutoVendidoDto>> ObterMaisVendidosAsync(
        IQueryable<Pedido> faturados,
        CancellationToken cancellationToken)
    {
        var linhas = await _consulta.ListarAsync(
            faturados
                .SelectMany(pedido => pedido.Itens)
                .GroupBy(item => new { item.IdProduto, item.NomeProdutoSnapshot })
                .Select(grupo => new
                {
                    grupo.Key.IdProduto,
                    grupo.Key.NomeProdutoSnapshot,
                    Quantidade = grupo.Sum(item => item.Quantidade),
                    Total = grupo.Sum(item => (long)item.TotalLinhaCentavos)
                })
                .OrderByDescending(linha => linha.Quantidade)
                .Take(LimitePadraoLista),
            cancellationToken);

        return
        [
            .. linhas.Select(linha => new DashboardProdutoVendidoDto
            {
                IdProduto = linha.IdProduto,
                NomeProduto = linha.NomeProdutoSnapshot,
                QuantidadeVendida = linha.Quantidade,
                TotalCentavos = EmInteiro(linha.Total)
            })
        ];
    }

    /// <summary>
    /// Alerta de reposicao. O criterio e DISPONIVEL (fisico menos reservado) contra a quantidade
    /// minima: peca comprometida em checkout aguardando pagamento nao pode ser vendida de novo, e
    /// olhar so o fisico faz o alerta mentir justamente na semana de maior giro.
    ///
    /// Variacoes inativas ficam de fora — alertar para repor SKU que nao esta a venda so gera ruido.
    /// </summary>
    private async Task<(int Total, IReadOnlyList<DashboardEstoqueBaixoDto> Itens)> ObterEstoqueBaixoAsync(
        CancellationToken cancellationToken)
    {
        var criticos = _estoques.Query()
            .Where(estoque =>
                estoque.QuantidadeMinima > 0
                && estoque.Quantidade - estoque.QuantidadeReservada <= estoque.QuantidadeMinima
                && estoque.Variacao.Ativo);

        var total = await _consulta.ContarAsync(criticos, cancellationToken);

        if (total == 0)
            return (0, []);

        var itens = await _consulta.ListarAsync(
            criticos
                .OrderBy(estoque => estoque.Quantidade - estoque.QuantidadeReservada)
                .ThenBy(estoque => estoque.IdVariacao)
                .Take(LimitePadraoLista)
                .Select(estoque => new DashboardEstoqueBaixoDto
                {
                    IdVariacao = estoque.IdVariacao,
                    Sku = estoque.Variacao.Sku,
                    IdProduto = estoque.Variacao.IdProduto,
                    NomeProduto = estoque.Variacao.Produto == null
                        ? string.Empty
                        : estoque.Variacao.Produto.Nome,
                    Tamanho = estoque.Variacao.Tamanho == null
                        ? string.Empty
                        : estoque.Variacao.Tamanho.Codigo,
                    Cor = estoque.Variacao.Cor == null
                        ? string.Empty
                        : estoque.Variacao.Cor.Nome,
                    Quantidade = estoque.Quantidade,
                    QuantidadeReservada = estoque.QuantidadeReservada,
                    Disponivel = estoque.Quantidade - estoque.QuantidadeReservada,
                    QuantidadeMinima = estoque.QuantidadeMinima
                }),
            cancellationToken);

        return (total, itens);
    }

    /// <summary>
    /// Fila de envio travada.
    ///
    /// Entra o que ja falhou de vez (StatusEnvio.Falha) e tambem o que continua pendente depois de
    /// pelo menos uma tentativa. Esperar virar Falha para avisar significa descobrir o problema
    /// depois do backoff exponencial inteiro ter rodado, com o cliente ja cobrando o rastreio.
    /// </summary>
    private async Task<(int Total, IReadOnlyList<DashboardEnvioProblemaDto> Itens)> ObterEnviosComProblemaAsync(
        CancellationToken cancellationToken)
    {
        var problemas = _envios.Query()
            .Where(envio =>
                envio.Status == StatusEnvio.Falha
                || (envio.Tentativas > 0 && StatusEnvioEmAndamento.Contains(envio.Status)));

        var total = await _consulta.ContarAsync(problemas, cancellationToken);

        if (total == 0)
            return (0, []);

        var linhas = await _consulta.ListarAsync(
            problemas
                .OrderByDescending(envio => envio.Tentativas)
                .ThenBy(envio => envio.IdPedido)
                .Take(LimitePadraoLista)
                .Select(envio => new
                {
                    IdEnvio = envio.Id,
                    envio.IdPedido,
                    NumeroPedido = envio.Pedido == null ? string.Empty : envio.Pedido.Numero,
                    envio.Status,
                    envio.Tentativas,
                    envio.UltimoErro,
                    envio.ProximaTentativaEm
                }),
            cancellationToken);

        return (
            total,
            [
                .. linhas.Select(linha => new DashboardEnvioProblemaDto
                {
                    IdEnvio = linha.IdEnvio,
                    IdPedido = linha.IdPedido,
                    NumeroPedido = linha.NumeroPedido,
                    Status = linha.Status,
                    StatusNome = linha.Status.ToString(),
                    Tentativas = linha.Tentativas,
                    UltimoErro = linha.UltimoErro,
                    ProximaTentativaEm = linha.ProximaTentativaEm
                })
            ]);
    }

    /// <summary>
    /// Converte a soma em long de volta para o int de centavos do contrato, saturando no teto.
    /// Saturar e melhor que estourar: um painel que mostra o teto avisa que algo esta fora da
    /// escala, enquanto um overflow silencioso mostra faturamento negativo.
    /// </summary>
    private static int EmInteiro(long valor) =>
        valor >= int.MaxValue ? int.MaxValue : valor <= int.MinValue ? int.MinValue : (int)valor;
}
