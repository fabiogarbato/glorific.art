using Glorific.Application.Models.Pagamento;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Application.Services;

/// <summary>
/// Confirmacao de pagamento — o ponto mais sensivel do sistema inteiro.
///
/// A InfinitePay nao assina o webhook e o retorno do cliente e um GET que qualquer pessoa monta.
/// Portanto NADA que chega de fora e prova. As tres regras que este servico materializa:
///
/// 1. O EVENTO E GRAVADO PRIMEIRO. A unique em provider_event_id transforma reentrega em 200
///    imediato, e o payload cru fica auditavel. O repo de referencia usava um if de status dentro
///    do handler: cobria o caso feliz e nada mais.
///
/// 2. SO APROVA SE O GATEWAY CONFIRMAR. ConsultarPagamentoAsync e a unica fonte da verdade. No
///    repo de referencia a conferencia era chamada e o resultado descartado num catch — quem
///    descobrisse um order_nsu quitava pedido de graca.
///
/// 3. SO APROVA SE O VALOR BATER, em centavos, com o total do pedido. Divergencia nao aprova, nao
///    recusa: marca para revisao manual e loga. Pagamento parcial nao libera mercadoria.
///
/// E uma regra de execucao: e-mail roda FORA da transacao. No repo de referencia o SMTP era
/// chamado dentro dela, e servidor de e-mail fora do ar dava rollback em pagamento ja confirmado.
/// </summary>
public sealed class PagamentoService : IPagamentoService
{
    /// <summary>
    /// Contrato de fronteira com o adaptador do gateway (InfinitePayGateway expoe as mesmas
    /// constantes). Esta camada nao referencia a Infrastructure, entao a combinacao acordada e
    /// Encontrado = false somado a este StatusOriginal.
    ///
    /// Distinguir os dois casos importa: "nao conheco a transacao" e provavelmente aviso forjado
    /// e encerra o assunto; "nao consegui falar" e indefinicao e TEM que virar nova tentativa,
    /// nunca cancelamento de um pedido que pode ter sido pago.
    /// </summary>
    private const string StatusGatewayNaoEncontrado = "nao-encontrado";

    private const string StatusGatewayFalhaTransporte = "falha-de-transporte";

    private readonly IPagamentoRepository _pagamentos;
    private readonly IPedidoRepository _pedidos;
    private readonly IEstoqueService _estoques;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPaymentGateway _gateway;
    private readonly IEnvioService _envios;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;
    private readonly AppOptions _app;
    private readonly ILogger<PagamentoService> _logger;

    public PagamentoService(
        IPagamentoRepository pagamentos,
        IPedidoRepository pedidos,
        IEstoqueService estoques,
        IUsuarioRepository usuarios,
        IPaymentGateway gateway,
        IEnvioService envios,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IClock relogio,
        IOptions<AppOptions> app,
        ILogger<PagamentoService> logger)
    {
        _pagamentos = pagamentos ?? throw new ArgumentNullException(nameof(pagamentos));
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _envios = envios ?? throw new ArgumentNullException(nameof(envios));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _app = app?.Value ?? throw new ArgumentNullException(nameof(app));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ResultadoAvisoPagamento> ReceberAvisoAsync(
        WebhookPagamentoInfo aviso,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aviso);

        // Localiza o pagamento so para amarrar o evento. Nenhuma decisao de negocio sai daqui:
        // o aviso pode nem corresponder a uma cobranca nossa.
        var pagamento = await LocalizarPagamentoAsync(aviso, cancellationToken);

        var evento = new PagamentoEvento
        {
            IdPagamento = pagamento?.Id,
            ProviderEventId = aviso.ProviderEventId,
            Tipo = string.IsNullOrWhiteSpace(aviso.Slug) ? "pagamento.aviso" : $"pagamento.{aviso.Slug}",
            Payload = aviso.Payload,
            RecebidoEm = _relogio.UtcNow
        };

        // PRIMEIRO passo, sempre. O banco e o arbitro da idempotencia: reentrega volta false e o
        // controller responde 200 sem reprocessar nada.
        if (!await _pagamentos.TentarRegistrarEventoAsync(evento, cancellationToken))
        {
            _logger.LogInformation(
                "Aviso de pagamento reentregue e ignorado. ProviderEventId={EventoId}",
                aviso.ProviderEventId);

            return ResultadoAvisoPagamento.Duplicado;
        }

        if (pagamento is null)
        {
            // Aviso que nao casa com cobranca nenhuma. Fica gravado justamente para investigar:
            // com order_nsu nao enumeravel, isto e sinal de tentativa de forja.
            _logger.LogWarning(
                "Aviso de pagamento sem cobranca correspondente. OrderNsu={OrderNsu}",
                aviso.OrderNsu);

            evento.ProcessadoEm = _relogio.UtcNow;
            evento.Erro = "Nenhuma cobranca corresponde a este order_nsu.";
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ResultadoAvisoPagamento.PagamentoNaoEncontrado;
        }

        return await ConferirEDecidirAsync(
            pagamento.Id, aviso.TransactionNsu, aviso.Slug, evento, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ProcessarEventosPendentesAsync(
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return 0;

        var eventos = await _pagamentos.ObterEventosNaoProcessadosAsync(limite, cancellationToken);

        var concluidos = 0;

        foreach (var evento in eventos)
        {
            // Todo caminho que NAO termina em indefinicao precisa carimbar ProcessadoEm. A fila e
            // um WHERE processado_em IS NULL ordenado por data com LIMIT: evento que sai daqui sem
            // carimbo volta no topo do proximo lote para sempre e segura os eventos novos atras
            // dele. So a indefinicao (gateway fora do ar) fica sem carimbo, que e o ponto dela.
            if (evento.IdPagamento is null)
            {
                await MarcarEventoAsync(evento, "Aviso sem cobranca correspondente.", cancellationToken);
                continue;
            }

            var pagamento = await _pagamentos.ObterPorIdAsync(evento.IdPagamento.Value, cancellationToken);

            // So vale reconferir o que ainda esta em aberto. Cobranca ja resolvida nao precisa de
            // nova ida ao gateway, e insistir nela transformaria a fila num gerador de trafego.
            if (pagamento is null || pagamento.Status != StatusPagamento.Pendente)
            {
                await MarcarEventoAsync(evento, null, cancellationToken);
                continue;
            }

            // O evento vai RASTREADO daqui para baixo, entao ConferirEDecidirAsync carimba
            // ProcessadoEm pelo mesmo caminho do webhook — inclusive o Erro da divergencia de
            // valor, que antes se perdia neste fluxo.
            var resultado = await ConferirEDecidirAsync(
                pagamento.Id, null, pagamento.Metodo, evento, cancellationToken);

            if (resultado is ResultadoAvisoPagamento.Aprovado or ResultadoAvisoPagamento.NaoAprovado)
                concluidos++;
        }

        return concluidos;
    }

    /// <inheritdoc />
    public async Task<int> ExpirarPendentesAsync(int limite, CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return 0;

        var agora = _relogio.UtcNow;
        var vencidos = await _pagamentos.ObterExpiradosAsync(agora, limite, cancellationToken);

        var expirados = 0;

        foreach (var vencido in vencidos)
        {
            // Ultima conferencia antes de cancelar: o cliente pode ter pago no ultimo minuto e o
            // webhook ter se perdido. Cancelar um pedido pago e o pior erro possivel aqui.
            var consulta = await _gateway.ConsultarPagamentoAsync(
                vencido.ProviderOrderId ?? string.Empty,
                slug: vencido.Metodo,
                ct: cancellationToken);

            if (consulta.Encontrado && consulta.Status == StatusPagamentoGateway.Aprovado)
            {
                await ConferirEDecidirAsync(vencido.Id, null, vencido.Metodo, evento: null, cancellationToken);
                continue;
            }

            if (!consulta.Encontrado && consulta.StatusOriginal == StatusGatewayFalhaTransporte)
            {
                // Gateway fora do ar: nao decide nada. O pedido continua pendente e o proximo
                // ciclo tenta de novo.
                _logger.LogWarning(
                    "Expiracao adiada: gateway indisponivel. Pagamento={Pagamento}",
                    vencido.Id);

                continue;
            }

            if (await EncerrarSemAprovacaoAsync(
                    vencido.Id,
                    StatusPagamento.Expirado,
                    StatusPedido.Cancelado,
                    "Pagamento nao confirmado dentro do prazo.",
                    consulta.RawJson,
                    cancellationToken))
            {
                expirados++;
            }
        }

        return expirados;
    }

    // ------------------------------------------------------------------
    // Nucleo da decisao
    // ------------------------------------------------------------------

    private async Task<Pagamento?> LocalizarPagamentoAsync(
        WebhookPagamentoInfo aviso,
        CancellationToken cancellationToken)
    {
        var pagamento = await _pagamentos.ObterPorProviderOrderIdAsync(aviso.OrderNsu, cancellationToken);

        if (pagamento is not null || string.IsNullOrWhiteSpace(aviso.TransactionNsu))
            return pagamento;

        // O provedor ora manda o id do pedido, ora o da transacao. Procurar so por um deixaria
        // evento orfao esperando um pagamento que existe.
        return await _pagamentos.ObterPorProviderChargeIdAsync(aviso.TransactionNsu, cancellationToken);
    }

    /// <summary>
    /// Consulta o gateway e aplica o desfecho. Este metodo e a unica porta por onde um pedido
    /// vira Pago.
    /// </summary>
    /// <param name="evento">
    /// Instancia RASTREADA do evento recem-inserido, para carimbar ProcessadoEm no mesmo
    /// SaveChanges. Null quando a chamada vem de uma reconferencia (ver a nota de limitacao).
    /// </param>
    private async Task<ResultadoAvisoPagamento> ConferirEDecidirAsync(
        int idPagamento,
        string? transactionNsu,
        string? slug,
        PagamentoEvento? evento,
        CancellationToken cancellationToken)
    {
        var pagamento = await _pagamentos.ObterPorIdAsync(idPagamento, cancellationToken);

        if (pagamento is null)
            return ResultadoAvisoPagamento.PagamentoNaoEncontrado;

        var pedido = await _pedidos.ObterCompletoAsync(pagamento.IdPedido, cancellationToken);

        if (pedido is null)
        {
            _logger.LogError(
                "Pagamento {Pagamento} aponta para pedido inexistente {Pedido}.",
                pagamento.Id,
                pagamento.IdPedido);

            // Carimba porque nao ha o que reprocessar: sem pedido, nova tentativa daria o mesmo
            // erro e o evento ficaria travando a cabeca da fila indefinidamente.
            await MarcarEventoAsync(evento, "Pagamento sem pedido correspondente.", cancellationToken);

            return ResultadoAvisoPagamento.PagamentoNaoEncontrado;
        }

        var consulta = await _gateway.ConsultarPagamentoAsync(
            pagamento.ProviderOrderId ?? string.Empty, transactionNsu, slug, cancellationToken);

        if (!consulta.Encontrado)
        {
            if (consulta.StatusOriginal == StatusGatewayNaoEncontrado)
            {
                _logger.LogWarning(
                    "Gateway nao conhece a transacao do pedido {Numero}. Nada foi alterado.",
                    pedido.Numero);

                await MarcarEventoAsync(evento, "Gateway nao conhece a transacao.", cancellationToken);
                return ResultadoAvisoPagamento.PagamentoNaoEncontrado;
            }

            // Indefinicao. O evento fica SEM ProcessadoEm de proposito, para nova tentativa.
            _logger.LogWarning(
                "Conferencia inconclusiva do pedido {Numero}. Sera reprocessada.",
                pedido.Numero);

            return ResultadoAvisoPagamento.Inconclusivo;
        }

        if (!consulta.Aprovado)
        {
            return await TratarNaoAprovadoAsync(pagamento, pedido, consulta, evento, cancellationToken);
        }

        // Regra que o repo de referencia nao tinha: valor conferido em CENTAVOS, sem margem.
        if (!consulta.ValorConfere(pedido.TotalCentavos))
        {
            _logger.LogError(
                "DIVERGENCIA DE VALOR no pedido {Numero}. Esperado={Esperado} Recebido={Recebido} " +
                "OrderNsu={OrderNsu}. Pedido NAO aprovado; revisao manual necessaria.",
                pedido.Numero,
                pedido.TotalCentavos,
                consulta.ValorCentavos,
                pagamento.ProviderOrderId);

            await MarcarDivergenciaAsync(pagamento, pedido, consulta, evento, cancellationToken);
            return ResultadoAvisoPagamento.DivergenciaDeValor;
        }

        return await AprovarAsync(pagamento, pedido, consulta, evento, cancellationToken);
    }

    /// <summary>
    /// Aprovacao efetiva. Transacao curta: muda estados, EFETIVA o estoque (reserva vira venda) e
    /// enfileira o envio. Nenhuma chamada a parceiro externo acontece aqui dentro.
    /// </summary>
    private async Task<ResultadoAvisoPagamento> AprovarAsync(
        Pagamento pagamentoLeitura,
        Pedido pedidoLeitura,
        ConsultaPagamentoInfo consulta,
        PagamentoEvento? evento,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var pedido = await _pedidos.ObterParaEdicaoAsync(pedidoLeitura.Id, cancellationToken);
            var pagamento = await _pagamentos.ObterParaEdicaoAsync(pagamentoLeitura.Id, cancellationToken);

            if (pedido is null || pagamento is null)
                return ResultadoAvisoPagamento.PagamentoNaoEncontrado;

            // Guarda de idempotencia nivel 1: webhook e retorno do navegador chegam juntos e cada
            // um traz um provider_event_id diferente, entao a unique do evento nao cobre este par.
            if (pedido.Status != StatusPedido.AguardandoPagamento)
            {
                _logger.LogInformation(
                    "Pedido {Numero} ja saiu de AguardandoPagamento ({Status}). Aprovacao ignorada.",
                    pedido.Numero,
                    pedido.Status);

                await MarcarEventoAsync(evento, null, cancellationToken);
                await transacao.CommitAsync(cancellationToken);

                return ResultadoAvisoPagamento.Aprovado;
            }

            pagamento.Status = StatusPagamento.Aprovado;
            pagamento.Metodo = consulta.Metodo ?? pagamento.Metodo;
            pagamento.Parcelas = consulta.Parcelas ?? pagamento.Parcelas;
            pagamento.ProviderChargeId = consulta.TransactionNsu ?? pagamento.ProviderChargeId;
            pagamento.DataConfirmacao = consulta.PagoEmUtc ?? agora;
            pagamento.RawUltimaResposta = consulta.RawJson;
            _pagamentos.Atualizar(pagamento);

            var statusAnterior = pedido.Status;
            pedido.Status = StatusPedido.Pago;
            pedido.DataPagamento = pagamento.DataConfirmacao;
            _pedidos.Atualizar(pedido);

            await _pedidos.RegistrarHistoricoAsync(
                new PedidoHistorico
                {
                    IdPedido = pedido.Id,
                    StatusAnterior = statusAnterior,
                    StatusNovo = StatusPedido.Pago,
                    IdUsuario = null,
                    Observacao = $"Pagamento confirmado no gateway ({consulta.Metodo ?? "desconhecido"}).",
                    DataAlteracao = agora
                },
                cancellationToken);

            await EfetivarEstoqueAsync(pedido, pedidoLeitura.Itens, cancellationToken);

            // Apenas o INSERT em envios. Nada e chamado no Melhor Envio aqui: quem fala com o
            // parceiro e o worker, fora de qualquer transacao.
            await _envios.EnfileirarAsync(pedido.Id, cancellationToken);

            await MarcarEventoAsync(evento, null, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }

        // FORA da transacao, e de proposito. SMTP fora do ar nao pode derrubar pagamento ja
        // confirmado — foi exatamente esse o incidente no repo de referencia.
        await NotificarClienteAsync(pedidoLeitura, consulta, cancellationToken);

        _logger.LogInformation(
            "Pedido {Numero} confirmado. Valor={Valor} Metodo={Metodo}",
            pedidoLeitura.Numero,
            consulta.ValorCentavos,
            consulta.Metodo);

        return ResultadoAvisoPagamento.Aprovado;
    }

    /// <summary>
    /// Reserva vira venda: baixa o fisico e a reserva na MESMA instrucao, por item. A primitiva
    /// do EstoqueService nao commita, entao ela participa da transacao aberta acima.
    ///
    /// Guarda de idempotencia nivel 2: a efetivacao exige reservada maior ou igual a quantidade.
    /// Numa segunda execucao a reserva ja nao existe, o UPDATE afeta zero linhas e nada e
    /// decrementado duas vezes. Falha aqui vira alerta e nao erro fatal: o dinheiro ja entrou, e
    /// segurar o pedido por causa do estoque so piora a situacao para todo mundo.
    /// </summary>
    private async Task EfetivarEstoqueAsync(
        Pedido pedido,
        IEnumerable<PedidoItem> itens,
        CancellationToken cancellationToken)
    {
        foreach (var item in itens)
        {
            var resultado = await _estoques.EfetivarVendaAsync(
                item.IdVariacao, item.Quantidade, pedido.Id, cancellationToken);

            if (resultado.Falhou)
            {
                _logger.LogWarning(
                    "Efetivacao de estoque sem efeito no pedido {Numero}, variacao {Variacao}: {Erro}",
                    pedido.Numero,
                    item.IdVariacao,
                    resultado.Erro);
            }
        }
    }

    /// <summary>
    /// Gateway respondeu, mas nao aprovou. Recusa, expiracao e cancelamento LIBERAM a reserva —
    /// deixar a peca presa aqui e como o estoque some do site sem ninguem ter comprado.
    /// Pendente nao muda nada: e so um aviso de que a cobranca continua aberta.
    /// </summary>
    private async Task<ResultadoAvisoPagamento> TratarNaoAprovadoAsync(
        Pagamento pagamento,
        Pedido pedido,
        ConsultaPagamentoInfo consulta,
        PagamentoEvento? evento,
        CancellationToken cancellationToken)
    {
        if (consulta.Status == StatusPagamentoGateway.Desconhecido)
        {
            // Status novo do provedor. Nao decidir e a decisao certa.
            _logger.LogWarning(
                "Status desconhecido do gateway no pedido {Numero}: {StatusOriginal}. Nada alterado.",
                pedido.Numero,
                consulta.StatusOriginal);

            return ResultadoAvisoPagamento.Inconclusivo;
        }

        if (consulta.Status == StatusPagamentoGateway.Pendente)
        {
            await MarcarEventoAsync(evento, "Cobranca ainda pendente no gateway.", cancellationToken);
            return ResultadoAvisoPagamento.NaoAprovado;
        }

        var statusPedido = consulta.Status switch
        {
            StatusPagamentoGateway.Recusado => StatusPedido.PagamentoRecusado,
            StatusPagamentoGateway.Estornado => StatusPedido.Estornado,
            _ => StatusPedido.Cancelado
        };

        await EncerrarSemAprovacaoAsync(
            pagamento.Id,
            consulta.ParaStatusDominio(),
            statusPedido,
            $"Gateway respondeu {consulta.StatusOriginal ?? consulta.Status.ToString()}.",
            consulta.RawJson,
            cancellationToken);

        await MarcarEventoAsync(evento, null, cancellationToken);

        return ResultadoAvisoPagamento.NaoAprovado;
    }

    /// <summary>
    /// Encerra a cobranca sem aprovar e devolve a reserva de estoque. Usado por recusa, estorno,
    /// cancelamento e expiracao. Idempotente: pedido que ja saiu de AguardandoPagamento nao e
    /// tocado, e LiberarReservaAsync exige reserva existente para afetar linha.
    /// </summary>
    private async Task<bool> EncerrarSemAprovacaoAsync(
        int idPagamento,
        StatusPagamento statusPagamento,
        StatusPedido statusPedido,
        string motivo,
        string? raw,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var pagamento = await _pagamentos.ObterParaEdicaoAsync(idPagamento, cancellationToken);

            if (pagamento is null)
                return false;

            var pedidoLeitura = await _pedidos.ObterCompletoAsync(pagamento.IdPedido, cancellationToken);
            var pedido = await _pedidos.ObterParaEdicaoAsync(pagamento.IdPedido, cancellationToken);

            if (pedidoLeitura is null || pedido is null)
                return false;

            if (pedido.Status != StatusPedido.AguardandoPagamento)
            {
                await transacao.CommitAsync(cancellationToken);
                return false;
            }

            pagamento.Status = statusPagamento;
            pagamento.RawUltimaResposta = raw ?? pagamento.RawUltimaResposta;
            _pagamentos.Atualizar(pagamento);

            var statusAnterior = pedido.Status;
            pedido.Status = statusPedido;
            pedido.DataCancelamento = agora;
            pedido.MotivoCancelamento = motivo;
            _pedidos.Atualizar(pedido);

            await _pedidos.RegistrarHistoricoAsync(
                new PedidoHistorico
                {
                    IdPedido = pedido.Id,
                    StatusAnterior = statusAnterior,
                    StatusNovo = statusPedido,
                    IdUsuario = null,
                    Observacao = motivo,
                    DataAlteracao = agora
                },
                cancellationToken);

            await LiberarReservasAsync(pedido, pedidoLeitura.Itens, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Pedido {Numero} encerrado sem aprovacao: {Motivo}",
                pedido.Numero,
                motivo);

            return true;
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Devolve a reserva de cada item. A primitiva do EstoqueService ja e idempotente (exige
    /// reserva existente para afetar linha) e ja grava o ledger, entao uma reentrega do aviso de
    /// expiracao nao deixa a quantidade reservada negativa.
    /// </summary>
    private async Task LiberarReservasAsync(
        Pedido pedido,
        IEnumerable<PedidoItem> itens,
        CancellationToken cancellationToken)
    {
        foreach (var item in itens)
        {
            await _estoques.LiberarReservaAsync(
                item.IdVariacao,
                item.Quantidade,
                pedido.Id,
                idUsuario: null,
                $"Liberacao de reserva do pedido {pedido.Numero}.",
                cancellationToken);
        }
    }

    /// <summary>
    /// Valor pago diferente do total do pedido.
    ///
    /// NAO aprova e NAO cancela: mantem a cobranca pendente, deixa a resposta crua gravada e
    /// registra a ocorrencia no historico do pedido para alguem olhar. Aprovar valor menor
    /// entrega mercadoria de graca; cancelar valor maior pune quem pagou.
    /// </summary>
    private async Task MarcarDivergenciaAsync(
        Pagamento pagamentoLeitura,
        Pedido pedidoLeitura,
        ConsultaPagamentoInfo consulta,
        PagamentoEvento? evento,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        var pagamento = await _pagamentos.ObterParaEdicaoAsync(pagamentoLeitura.Id, cancellationToken);

        if (pagamento is not null)
        {
            pagamento.RawUltimaResposta = consulta.RawJson;
            _pagamentos.Atualizar(pagamento);
        }

        var observacao =
            $"REVISAO MANUAL: gateway informou {consulta.ValorCentavos} centavos e o pedido soma " +
            $"{pedidoLeitura.TotalCentavos}. Pagamento nao aprovado automaticamente.";

        await _pedidos.RegistrarHistoricoAsync(
            new PedidoHistorico
            {
                IdPedido = pedidoLeitura.Id,
                StatusAnterior = pedidoLeitura.Status,
                StatusNovo = pedidoLeitura.Status,
                IdUsuario = null,
                Observacao = observacao,
                DataAlteracao = agora
            },
            cancellationToken);

        if (evento is not null)
        {
            evento.ProcessadoEm = agora;
            evento.Erro = observacao;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await AlertarAdministracaoAsync(
            $"Divergencia de valor no pedido {pedidoLeitura.Numero}",
            observacao,
            cancellationToken);
    }

    private async Task MarcarEventoAsync(
        PagamentoEvento? evento,
        string? erro,
        CancellationToken cancellationToken)
    {
        if (evento is null)
            return;

        evento.ProcessadoEm = _relogio.UtcNow;
        evento.Erro = erro;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task NotificarClienteAsync(
        Pedido pedido,
        ConsultaPagamentoInfo consulta,
        CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ObterPorIdAsync(pedido.IdUsuario, cancellationToken);

        if (usuario is null || string.IsNullOrWhiteSpace(usuario.Email))
            return;

        var comprovante = string.IsNullOrWhiteSpace(consulta.UrlComprovante)
            ? string.Empty
            : $"<p>Comprovante: <a href=\"{consulta.UrlComprovante}\">{consulta.UrlComprovante}</a></p>";

        var corpo =
            $"<p>Recebemos o pagamento do seu pedido <strong>{pedido.Numero}</strong>.</p>" +
            $"<p>Acompanhe em <a href=\"{_app.UrlLoja($"conta/pedidos/{pedido.Uuid}")}\">" +
            $"{_app.UrlLoja($"conta/pedidos/{pedido.Uuid}")}</a>.</p>" +
            comprovante;

        await EnviarSemQuebrarAsync(
            usuario.Email,
            $"{_app.NomeLoja} - pagamento confirmado ({pedido.Numero})",
            corpo,
            cancellationToken);
    }

    private Task AlertarAdministracaoAsync(string assunto, string corpo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_app.EmailAdministrativo))
            return Task.CompletedTask;

        return EnviarSemQuebrarAsync(_app.EmailAdministrativo, assunto, $"<p>{corpo}</p>", cancellationToken);
    }

    /// <summary>
    /// E-mail nunca derruba o fluxo. Ja aconteceu no repo de referencia: SMTP indisponivel
    /// desfazendo pagamento confirmado.
    /// </summary>
    private async Task EnviarSemQuebrarAsync(
        string destinatario,
        string assunto,
        string corpoHtml,
        CancellationToken cancellationToken)
    {
        try
        {
            await _email.EnviarAsync(destinatario, assunto, corpoHtml, cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogWarning(excecao, "Falha ao enviar e-mail para {Destinatario}.", destinatario);
        }
    }
}
