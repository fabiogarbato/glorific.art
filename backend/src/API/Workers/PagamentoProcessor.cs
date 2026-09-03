using Glorific.Application.Services.Interfaces;

namespace Glorific.Api.Workers;

/// <summary>
/// Worker de pagamento. Faz as duas tarefas que ninguem mais faz:
///
/// 1. DRENA A FILA DE EVENTOS. O webhook grava o evento e tenta conferir na hora; quando o
///    gateway esta fora do ar a conferencia sai Inconclusiva de proposito e o evento fica sem
///    ProcessadoEm. Sem alguem varrendo essa fila, o pagamento aprovado durante a indisponibilidade
///    NUNCA e confirmado — o cliente pagou e o pedido morre em AguardandoPagamento.
///
/// 2. EXPIRA COBRANCA VENCIDA E DEVOLVE A RESERVA DE ESTOQUE. Este era o buraco mais caro: o
///    checkout RESERVA a peca, o pix abandonado nunca vira pagamento e, sem expiracao, a reserva
///    fica de pe para sempre. A loja passa a recusar venda de peca que esta na prateleira.
///
/// ATENCAO — SINGLE INSTANCE POR DESIGN, pelo mesmo motivo do EnvioProcessor: roda dentro do
/// processo da API e duas replicas viram dois workers na mesma fila. As guardas de idempotencia do
/// PagamentoService (unique de provider_event_id, status do pedido e reserva exigida na
/// efetivacao) impedem cobranca dupla, mas nao o desperdicio de duas consultas ao gateway por
/// evento. Antes de escalar, use eleicao de lider por advisory lock ou mova para container proprio.
///
/// As mesmas duas regras de robustez do EnvioProcessor valem aqui: escopo novo a cada ciclo (o
/// BackgroundService e singleton, os servicos sao scoped) e try/catch no ciclo inteiro (excecao que
/// escapa de ExecuteAsync mata o worker em silencio e a fila para sem ninguem perceber).
/// </summary>
public sealed class PagamentoProcessor : BackgroundService
{
    /// <summary>
    /// Dois minutos. Mais curto que o do envio porque aqui o atraso e sentido pelo cliente, que
    /// esta olhando a tela de "confirmando pagamento"; mais longo que segundos porque o caminho
    /// feliz ja e resolvido pelo proprio webhook — este ciclo e a rede de seguranca.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(2);

    /// <summary>Espera curta apos falha do ciclo inteiro (banco fora do ar, por exemplo).</summary>
    private static readonly TimeSpan IntervaloAposFalha = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Lote por ciclo. Cada item vira UMA consulta ao gateway, entao o lote e o teto de chamadas
    /// externas por ciclo — 25 e o que cabe folgado dentro dos dois minutos sem virar rajada.
    /// </summary>
    private const int TamanhoDoLote = 25;

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<PagamentoProcessor> _logger;

    public PagamentoProcessor(IServiceScopeFactory escopos, ILogger<PagamentoProcessor> logger)
    {
        _escopos = escopos ?? throw new ArgumentNullException(nameof(escopos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PagamentoProcessor iniciado. Ciclo de {Segundos} s, lote de {Lote}.",
            Intervalo.TotalSeconds,
            TamanhoDoLote);

        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = Intervalo;

            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento normal do host. Nao e erro e nao deve poluir o alerta.
                break;
            }
            catch (Exception excecao)
            {
                // O worker NUNCA morre por causa de um ciclo ruim.
                _logger.LogError(excecao, "Ciclo do PagamentoProcessor falhou. O worker continua.");
                espera = IntervaloAposFalha;
            }

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PagamentoProcessor encerrado.");
    }

    /// <summary>
    /// Um ciclo, com escopo proprio para o DbContext viver o tempo do lote e nao o do processo.
    ///
    /// A ordem importa: primeiro conferir os eventos em aberto, depois expirar. Expirar antes
    /// cancelaria um pedido cujo comprovante de pagamento ja estava na fila esperando conferencia.
    /// </summary>
    private async Task ExecutarCicloAsync(CancellationToken stoppingToken)
    {
        await using var escopo = _escopos.CreateAsyncScope();

        var pagamentos = escopo.ServiceProvider.GetRequiredService<IPagamentoService>();

        var conferidos = await pagamentos.ProcessarEventosPendentesAsync(TamanhoDoLote, stoppingToken);

        if (conferidos > 0)
            _logger.LogInformation("PagamentoProcessor concluiu {Quantidade} evento(s).", conferidos);

        var expirados = await pagamentos.ExpirarPendentesAsync(TamanhoDoLote, stoppingToken);

        if (expirados > 0)
            _logger.LogInformation(
                "PagamentoProcessor expirou {Quantidade} cobranca(s) e devolveu a reserva de estoque.",
                expirados);
    }
}
