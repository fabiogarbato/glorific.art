using Glorific.Application.Common;
using Glorific.Application.DTO.Pedidos;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Glorific.Application.Services;

/// <summary>
/// Leitura de pedidos (cliente e painel) e as duas operacoes manuais da expedicao: mudar status e
/// cancelar.
///
/// Detalhe de implementacao que vale explicar: as listagens usam PROJECAO (Select) e nao Include.
/// Nao e estilo — esta camada nao referencia EF, entao Include nem existe aqui; e projetar tem o
/// efeito colateral desejavel de a consulta trazer so as colunas da tela, em vez do agregado
/// inteiro com tres niveis de navegacao como fazia o repo de referencia.
/// </summary>
public sealed class PedidoService : IPedidoService
{
    private readonly IPedidoRepository _pedidos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IEnvioRepository _envios;
    private readonly IEstoqueService _estoques;
    private readonly IEnvioService _envioService;
    private readonly IConsultaAssincrona _consulta;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IClock _relogio;
    private readonly ILogger<PedidoService> _logger;

    public PedidoService(
        IPedidoRepository pedidos,
        IUsuarioRepository usuarios,
        IEnvioRepository envios,
        IEstoqueService estoques,
        IEnvioService envioService,
        IConsultaAssincrona consulta,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IClock relogio,
        ILogger<PedidoService> logger)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _envios = envios ?? throw new ArgumentNullException(nameof(envios));
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
        _envioService = envioService ?? throw new ArgumentNullException(nameof(envioService));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PagedResult<PedidoResumoResponseDto>> ListarMeusAsync(
        string usuarioUuid,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var idUsuario = await ResolverUsuarioAsync(usuarioUuid, cancellationToken);

        // O filtro por usuario esta DENTRO da consulta do repositorio. Carregar tudo e filtrar em
        // memoria seria o caminho para um pedido alheio aparecer numa refatoracao distraida.
        var consulta = _pedidos.QueryDoUsuario(idUsuario);

        return await PaginarResumoAsync(consulta, requisicao, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> ObterMeuAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var idUsuario = await ResolverUsuarioAsync(usuarioUuid, cancellationToken);

        var pedido = await _pedidos.ObterDoUsuarioAsync(idUsuario, pedidoUuid, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        // exporEtiqueta: false. A URL da etiqueta e documento fiscal e operacional da loja; para o
        // cliente ela nao acrescenta nada e expoe dados do remetente.
        return Montar(pedido, exporEtiqueta: false);
    }

    /// <inheritdoc />
    public async Task<RastreioResponseDto> ObterRastreioAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var idUsuario = await ResolverUsuarioAsync(usuarioUuid, cancellationToken);

        var pedido = await _pedidos.ObterDoUsuarioAsync(idUsuario, pedidoUuid, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        var envio = pedido.Envio;

        if (envio is null)
        {
            return new RastreioResponseDto
            {
                NumeroPedido = pedido.Numero,
                StatusEnvio = StatusEnvio.Pendente.ToString(),
                Transportadora = pedido.TransportadoraFrete,
                Servico = pedido.ServicoFrete
            };
        }

        var eventos = await _envios.ObterEventosAsync(envio.Id, cancellationToken);

        return new RastreioResponseDto
        {
            NumeroPedido = pedido.Numero,
            StatusEnvio = envio.Status.ToString(),
            Transportadora = envio.NomeTransportadora ?? pedido.TransportadoraFrete,
            Servico = envio.NomeServico ?? pedido.ServicoFrete,
            CodigoRastreio = envio.CodigoRastreio,
            Eventos = [.. eventos.Select(evento => _mapper.Map<RastreioEventoResponseDto>(evento))]
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<PedidoResumoResponseDto>> ListarAdminAsync(
        PedidoFiltroAdminDto filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();
        filtro ??= new PedidoFiltroAdminDto();

        var consulta = _pedidos.Query();

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            var status = ConverterStatus(filtro.Status);
            consulta = consulta.Where(pedido => pedido.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            // Truncado no servico e nao so por DataAnnotation: o filtro e um contrato interno e
            // nao passa por validacao de ModelState. Sem o teto, uma busca de 100 KB viraria um
            // LIKE gigante que o banco varre inteiro.
            var busca = filtro.Busca.Trim();
            busca = busca.Length > 120 ? busca[..120] : busca;

            // Numero do pedido e nome do destinatario cobrem 100 por cento do que o atendimento
            // digita: o cliente liga com o numero em maos ou com o proprio nome.
            consulta = consulta.Where(pedido =>
                pedido.Numero.Contains(busca)
                || pedido.EnderecoEntrega.Destinatario.Contains(busca));
        }

        if (filtro.De is not null)
            consulta = consulta.Where(pedido => pedido.DataCriacao >= filtro.De.Value);

        if (filtro.Ate is not null)
            consulta = consulta.Where(pedido => pedido.DataCriacao <= filtro.Ate.Value);

        return await PaginarResumoAsync(
            consulta.OrderByDescending(pedido => pedido.DataCriacao).ThenByDescending(pedido => pedido.Id),
            requisicao,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> ObterAdminAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var pedido = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        return Montar(pedido, exporEtiqueta: true);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> AlterarStatusAsync(
        string pedidoUuid,
        AlterarStatusPedidoDto dto,
        string usuarioAdminUuid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var idAdmin = await ResolverUsuarioAsync(usuarioAdminUuid, cancellationToken);
        var novoStatus = ConverterStatus(dto.StatusNovo);

        var leitura = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        // Cancelamento tem efeito colateral em estoque e etiqueta. Forcar o caminho proprio evita
        // que alguem cancele por aqui e deixe a peca reservada para sempre.
        if (novoStatus == StatusPedido.Cancelado)
        {
            throw new BusinessValidationException(
                "Use o cancelamento do pedido: ele devolve estoque e cancela a etiqueta.");
        }

        var pedido = await _pedidos.ObterParaEdicaoAsync(leitura.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        if (pedido.Status == novoStatus)
            return Montar(leitura, exporEtiqueta: true);

        if (pedido.Status is StatusPedido.Cancelado or StatusPedido.Devolvido or StatusPedido.Estornado)
            throw new BusinessValidationException("Este pedido esta encerrado e nao aceita mudanca de status.");

        var anterior = pedido.Status;
        pedido.Status = novoStatus;

        var agora = _relogio.UtcNow;

        // Os carimbos de data acompanham o status: sem eles o relatorio de prazo de entrega vira
        // adivinhacao a partir do historico textual.
        switch (novoStatus)
        {
            case StatusPedido.Enviado:
                pedido.DataEnvio ??= agora;
                break;
            case StatusPedido.Entregue:
                pedido.DataEntrega ??= agora;
                break;
        }

        _pedidos.Atualizar(pedido);

        await _pedidos.RegistrarHistoricoAsync(
            new PedidoHistorico
            {
                IdPedido = pedido.Id,
                StatusAnterior = anterior,
                StatusNovo = novoStatus,
                IdUsuario = idAdmin,
                Observacao = dto.Observacao,
                DataAlteracao = agora
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var atualizado = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        return Montar(atualizado, exporEtiqueta: true);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> CancelarAsync(
        string pedidoUuid,
        CancelarPedidoDto dto,
        string usuarioAdminUuid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var idAdmin = await ResolverUsuarioAsync(usuarioAdminUuid, cancellationToken);
        var leitura = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        if (leitura.Status is StatusPedido.Cancelado or StatusPedido.Devolvido)
            throw new BusinessValidationException("Este pedido ja esta cancelado.");

        if (leitura.Status == StatusPedido.Entregue)
            throw new BusinessValidationException("Pedido entregue nao pode ser cancelado; abra uma devolucao.");

        // Cancelar a etiqueta e I/O de rede e acontece ANTES e FORA da transacao. Segurar lock de
        // banco durante chamada ao Melhor Envio trava a expedicao inteira quando ele fica lento.
        if (leitura.Envio is not null)
        {
            try
            {
                await _envioService.CancelarAsync(leitura.Id, dto.Motivo, cancellationToken);
            }
            catch (Exception excecao)
            {
                // Etiqueta nao cancelada e prejuizo de frete, nao impedimento de cancelar a venda.
                _logger.LogError(
                    excecao,
                    "Falha ao cancelar a etiqueta do pedido {Numero}. O cancelamento do pedido segue.",
                    leitura.Numero);
            }
        }

        var agora = _relogio.UtcNow;

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var pedido = await _pedidos.ObterParaEdicaoAsync(leitura.Id, cancellationToken)
                ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

            var anterior = pedido.Status;

            pedido.Status = StatusPedido.Cancelado;
            pedido.DataCancelamento = agora;
            pedido.MotivoCancelamento = dto.Motivo;

            _pedidos.Atualizar(pedido);

            await DevolverEstoqueAsync(pedido, leitura.Itens, anterior, idAdmin, cancellationToken);

            await _pedidos.RegistrarHistoricoAsync(
                new PedidoHistorico
                {
                    IdPedido = pedido.Id,
                    StatusAnterior = anterior,
                    StatusNovo = StatusPedido.Cancelado,
                    IdUsuario = idAdmin,
                    Observacao = dto.Motivo,
                    DataAlteracao = agora
                },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }

        var atualizado = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        return Montar(atualizado, exporEtiqueta: true);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> GerarEtiquetaAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var pedido = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        if (pedido.Envio is null)
        {
            throw new BusinessValidationException(
                "Este pedido ainda nao tem envio enfileirado. Confirme o pagamento primeiro.");
        }

        if (pedido.Envio.Status == StatusEnvio.AguardandoNota)
        {
            throw new BusinessValidationException(
                "Este envio aguarda a chave da nota fiscal antes de virar etiqueta.");
        }

        await _envioService.ProcessarAsync(pedido.Envio.Id, cancellationToken);

        var atualizado = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        return Montar(atualizado, exporEtiqueta: true);
    }

    /// <inheritdoc />
    public async Task<string?> ObterUrlEtiquetaAsync(
        string pedidoUuid,
        bool publico = false,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _pedidos.ObterPorUuidAsync(pedidoUuid, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        return await _envioService.ObterUrlEtiquetaAsync(pedido.Id, publico, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PedidoResponseDto> SincronizarRastreioAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var pedido = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        if (pedido.Envio is not null)
            await _envioService.AtualizarRastreioAsync(pedido.Envio.Id, cancellationToken);

        var atualizado = await ObterPorUuidCompletoAsync(pedidoUuid, cancellationToken);

        return Montar(atualizado, exporEtiqueta: true);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Devolucao de estoque depende do ESTAGIO em que o pedido estava.
    ///
    /// Antes do pagamento a peca esta apenas RESERVADA: devolver e liberar a reserva, o fisico
    /// nunca foi tocado. Depois do pagamento o fisico ja baixou: devolver e uma ENTRADA. Tratar
    /// os dois casos igual e o erro que faz o estoque divergir do inventario.
    /// </summary>
    private async Task DevolverEstoqueAsync(
        Pedido pedido,
        IEnumerable<PedidoItem> itens,
        StatusPedido statusAnterior,
        int idAdmin,
        CancellationToken cancellationToken)
    {
        // Antes do pagamento a peca esta apenas RESERVADA; depois dele o fisico ja baixou.
        var aindaReservado = statusAnterior == StatusPedido.AguardandoPagamento;

        var observacao = $"Cancelamento do pedido {pedido.Numero}.";

        foreach (var item in itens)
        {
            var resultado = aindaReservado
                ? await _estoques.LiberarReservaAsync(
                    item.IdVariacao, item.Quantidade, pedido.Id, idAdmin, observacao, cancellationToken)
                : await _estoques.DevolverAoEstoqueAsync(
                    item.IdVariacao, item.Quantidade, pedido.Id, idAdmin, observacao, cancellationToken);

            if (resultado.Falhou)
            {
                _logger.LogWarning(
                    "Devolucao de estoque sem efeito no pedido {Numero}, variacao {Variacao}: {Erro}",
                    pedido.Numero,
                    item.IdVariacao,
                    resultado.Erro);
            }
        }
    }

    private async Task<int> ResolverUsuarioAsync(string usuarioUuid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioUuid))
            throw new UnauthorizedAccessException("Token sem identificacao de usuario.");

        var usuario = await _usuarios.ObterPorUuidAsync(usuarioUuid, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuario do token nao existe mais.");

        return usuario.Id;
    }

    private async Task<Pedido> ObterPorUuidCompletoAsync(string pedidoUuid, CancellationToken cancellationToken)
    {
        var resumo = await _pedidos.ObterPorUuidAsync(pedidoUuid, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        return await _pedidos.ObterCompletoAsync(resumo.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);
    }

    /// <summary>
    /// Paginacao com COUNT antes do Skip/Take e PROJECAO da pagina.
    ///
    /// A projecao carrega o enum e nao o texto: converter status para string dentro da consulta
    /// obrigaria o provedor a traduzir ToString para SQL, coisa que ele nem sempre faz — e quando
    /// nao faz, a alternativa dele e trazer a tabela para a memoria.
    /// </summary>
    private async Task<PagedResult<PedidoResumoResponseDto>> PaginarResumoAsync(
        IQueryable<Pedido> consulta,
        PageRequest requisicao,
        CancellationToken cancellationToken)
    {
        var total = await _consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<PedidoResumoResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(pedido => new ProjecaoResumo
            {
                Uuid = pedido.Uuid,
                Numero = pedido.Numero,
                Status = pedido.Status,
                TotalCentavos = pedido.TotalCentavos,
                QuantidadeItens = pedido.Itens.Sum(item => item.Quantidade),
                ImagemUrl = pedido.Itens
                    .OrderBy(item => item.Id)
                    .Select(item => item.ImagemUrlSnapshot)
                    .FirstOrDefault(),
                DataCriacao = pedido.DataCriacao,
                DataPagamento = pedido.DataPagamento,
                CodigoRastreio = pedido.Envio == null ? null : pedido.Envio.CodigoRastreio
            });

        var linhas = await _consulta.ListarAsync(pagina, cancellationToken);

        var itens = linhas
            .Select(linha => new PedidoResumoResponseDto
            {
                Uuid = linha.Uuid,
                Numero = linha.Numero,
                Status = linha.Status.ToString(),
                TotalCentavos = linha.TotalCentavos,
                QuantidadeItens = linha.QuantidadeItens,
                ImagemUrl = linha.ImagemUrl,
                DataCriacao = linha.DataCriacao,
                DataPagamento = linha.DataPagamento,
                CodigoRastreio = linha.CodigoRastreio
            })
            .ToArray();

        return PagedResult<PedidoResumoResponseDto>.Criar(itens, requisicao, total);
    }

    /// <summary>
    /// Monta o recibo. O parametro exporEtiqueta e explicito e obrigatorio de proposito: e a
    /// unica diferenca entre a resposta do cliente e a do painel, e deixar isso implicito seria
    /// vazar link de etiqueta na primeira refatoracao.
    /// </summary>
    private PedidoResponseDto Montar(Pedido pedido, bool exporEtiqueta) =>
        new()
        {
            Uuid = pedido.Uuid,
            Numero = pedido.Numero,
            Status = pedido.Status.ToString(),
            SubtotalCentavos = pedido.SubtotalCentavos,
            DescontoCupomCentavos = pedido.DescontoCupomCentavos,
            FreteCentavos = pedido.FreteCentavos,
            TotalCentavos = pedido.TotalCentavos,
            CodigoCupom = pedido.CodigoCupomSnapshot,
            TransportadoraFrete = pedido.TransportadoraFrete,
            ServicoFrete = pedido.ServicoFrete,
            PrazoFreteDias = pedido.PrazoFreteDias,
            ObservacaoCliente = pedido.ObservacaoCliente,
            MotivoCancelamento = pedido.MotivoCancelamento,
            DataCriacao = pedido.DataCriacao,
            DataPagamento = pedido.DataPagamento,
            DataEnvio = pedido.DataEnvio,
            DataEntrega = pedido.DataEntrega,
            DataCancelamento = pedido.DataCancelamento,
            EnderecoEntrega = pedido.EnderecoEntrega is null
                ? null
                : _mapper.Map<PedidoEnderecoResponseDto>(pedido.EnderecoEntrega),
            Itens =
            [
                .. pedido.Itens
                    .OrderBy(item => item.Id)
                    .Select(item => _mapper.Map<PedidoItemResponseDto>(item))
            ],
            Pagamento = pedido.Pagamento is null ? null : new PedidoPagamentoResponseDto
            {
                Provedor = pedido.Pagamento.Provedor,
                Metodo = pedido.Pagamento.Metodo,
                Status = pedido.Pagamento.Status.ToString(),
                ValorCentavos = pedido.Pagamento.ValorCentavos,
                Parcelas = pedido.Pagamento.Parcelas,
                // Link so enquanto ha o que pagar: devolve-lo depois convida a pagar de novo.
                PaymentUrl = pedido.Pagamento.Status == StatusPagamento.Pendente
                    ? pedido.Pagamento.PaymentUrl
                    : null,
                QrCodePix = pedido.Pagamento.Status == StatusPagamento.Pendente
                    ? pedido.Pagamento.QrCodePix
                    : null,
                LinhaDigitavel = pedido.Pagamento.Status == StatusPagamento.Pendente
                    ? pedido.Pagamento.LinhaDigitavel
                    : null,
                ExpiraEm = pedido.Pagamento.ExpiraEm,
                DataConfirmacao = pedido.Pagamento.DataConfirmacao
            },
            Envio = pedido.Envio is null ? null : new PedidoEnvioResponseDto
            {
                Status = pedido.Envio.Status.ToString(),
                Transportadora = pedido.Envio.NomeTransportadora ?? pedido.TransportadoraFrete,
                Servico = pedido.Envio.NomeServico ?? pedido.ServicoFrete,
                CodigoRastreio = pedido.Envio.CodigoRastreio,
                UrlEtiqueta = exporEtiqueta ? pedido.Envio.UrlEtiqueta : null,
                PrazoDias = pedido.PrazoFreteDias,
                DataAlteracao = pedido.Envio.DataAlteracao
            },
            Historico =
            [
                .. pedido.Historico
                    .OrderByDescending(historico => historico.DataAlteracao)
                    .ThenByDescending(historico => historico.Id)
                    .Select(historico => _mapper.Map<PedidoHistoricoResponseDto>(historico))
            ]
        };

    /// <summary>
    /// Converte o nome do status vindo do painel. Valor invalido e erro de negocio (400), nao
    /// excecao de parse virando 500 — e Enum.Parse sem validacao aceitaria ate "99".
    /// </summary>
    private static StatusPedido ConverterStatus(string valor)
    {
        if (Enum.TryParse<StatusPedido>(valor, ignoreCase: true, out var status)
            && Enum.IsDefined(status))
        {
            return status;
        }

        var validos = string.Join(", ", Enum.GetNames<StatusPedido>());

        throw new BusinessValidationException($"Status invalido. Use um destes: {validos}.");
    }

    /// <summary>
    /// Forma intermediaria da projecao. Existe para o enum atravessar a fronteira do banco sem
    /// virar string dentro do SQL.
    ///
    /// Propriedades com set comum, e nao init: o corpo desta projecao vira arvore de expressao
    /// para o provedor de consulta traduzir, e init-only em inicializador de objeto dentro de
    /// arvore de expressao e terreno onde o compilador ja recusou em versoes anteriores.
    /// </summary>
    private sealed class ProjecaoResumo
    {
        public string Uuid { get; set; } = string.Empty;

        public string Numero { get; set; } = string.Empty;

        public StatusPedido Status { get; set; }

        public int TotalCentavos { get; set; }

        public int QuantidadeItens { get; set; }

        public string? ImagemUrl { get; set; }

        public DateTime DataCriacao { get; set; }

        public DateTime? DataPagamento { get; set; }

        public string? CodigoRastreio { get; set; }
    }
}
