using Glorific.Application.Services.Interfaces;

namespace Glorific.Api.Workers;

/// <summary>
/// Worker que contrata as etiquetas no Melhor Envio.
///
/// ATENCAO — SINGLE INSTANCE POR DESIGN. Este worker roda dentro do processo da API. Enquanto a
/// API tiver UMA replica, isso e simples e funciona. Subir a API para duas replicas passa a rodar
/// dois workers concorrentes: o claim atomico de EnvioRepository.TentarReivindicarAsync impede
/// que os dois comprem a MESMA etiqueta, mas nao impede o desperdicio de dois processos disputando
/// a mesma fila, e qualquer passo futuro sem claim (varredura de rastreio, alertas) passaria a
/// duplicar. Antes de escalar horizontalmente, escolha um destes caminhos:
///   1. eleicao de lider (advisory lock do Postgres tomado no inicio de cada ciclo), ou
///   2. mover o worker para um container proprio com replicas = 1.
///
/// Duas regras de robustez estao materializadas abaixo:
///
/// 1. ESCOPO NOVO A CADA CICLO. O BackgroundService e singleton e os servicos sao scoped; resolver
///    um servico scoped uma vez e guardar significaria manter o mesmo DbContext vivo pela vida do
///    processo, acumulando ChangeTracker ate o container morrer de memoria.
///
/// 2. TRY/CATCH NO CICLO INTEIRO. Excecao que escapa de ExecuteAsync mata o BackgroundService em
///    silencio: a API continua respondendo, a fila para de andar e ninguem percebe ate um cliente
///    reclamar que o pedido nao foi postado.
/// </summary>
public sealed class EnvioProcessor : BackgroundService
{
    /// <summary>
    /// Intervalo entre ciclos. Sessenta segundos e o equilibrio entre postar cedo e nao
    /// martelar o parceiro: a compra de etiqueta nao e sensivel a minutos.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Espera curta apos falha do ciclo inteiro (banco fora do ar, por exemplo). Nao adianta
    /// voltar em um segundo, e nao adianta esperar o ciclo cheio.
    /// </summary>
    private static readonly TimeSpan IntervaloAposFalha = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Envios por ciclo. Lote pequeno de proposito: cada envio faz de uma a quatro chamadas HTTP
    /// ao Melhor Envio, e um lote grande transformaria um ciclo em varios minutos de I/O.
    /// </summary>
    private const int TamanhoDoLote = 10;

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<EnvioProcessor> _logger;

    public EnvioProcessor(IServiceScopeFactory escopos, ILogger<EnvioProcessor> logger)
    {
        _escopos = escopos ?? throw new ArgumentNullException(nameof(escopos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EnvioProcessor iniciado. Ciclo de {Segundos} s, lote de {Lote} envios.",
            Intervalo.TotalSeconds,
            TamanhoDoLote);

        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = Intervalo;

            try
            {
                var processados = await ExecutarCicloAsync(stoppingToken);

                if (processados > 0)
                    _logger.LogInformation("EnvioProcessor avancou {Quantidade} envio(s).", processados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento normal do host. Nao e erro e nao deve poluir o alerta.
                break;
            }
            catch (Exception excecao)
            {
                // O worker NUNCA morre por causa de um ciclo ruim.
                _logger.LogError(excecao, "Ciclo do EnvioProcessor falhou. O worker continua.");
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

        _logger.LogInformation("EnvioProcessor encerrado.");
    }

    /// <summary>
    /// Um ciclo. O escopo e criado e descartado aqui dentro, entao o DbContext vive o tempo do
    /// lote e nao o tempo do processo.
    /// </summary>
    private async Task<int> ExecutarCicloAsync(CancellationToken stoppingToken)
    {
        await using var escopo = _escopos.CreateAsyncScope();

        var envios = escopo.ServiceProvider.GetRequiredService<IEnvioService>();

        // ProcessarPendentesAsync ja trata a falha de CADA envio isoladamente: um pedido com dado
        // corrompido nao pode impedir os outros nove do lote de serem postados.
        return await envios.ProcessarPendentesAsync(TamanhoDoLote, stoppingToken);
    }
}
