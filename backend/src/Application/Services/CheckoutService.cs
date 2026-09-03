using Glorific.Application.DTO.Checkout;
using Glorific.Application.DTO.Frete;
using Glorific.Application.DTO.Promocoes;
using Glorific.Application.Exceptions;
using Glorific.Application.Models.Pagamento;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CarrinhoEntity = Glorific.Domain.Entities.Carrinho.Carrinho;
using CarrinhoItemEntity = Glorific.Domain.Entities.Carrinho.CarrinhoItem;

namespace Glorific.Application.Services;

/// <summary>
/// O checkout inteiro em uma transacao.
///
/// Nada do que o cliente manda vira dinheiro sem passar por revalidacao aqui: o corpo da
/// requisicao carrega apenas QUAL endereco e QUAL servico de frete. Preco vem do catalogo, frete
/// vem de uma RECOTACAO feita neste instante, desconto vem do cupom consumido atomicamente. O
/// repo de referencia aceitava o valor do frete vindo do navegador — bastava trocar um numero no
/// devtools.
///
/// Este servico ORQUESTRA e nao reimplementa: recotacao e do FreteService, regra e consumo de
/// cupom sao do CupomService, reserva de saldo e ledger sao do EstoqueService. O que e exclusivo
/// daqui e a composicao transacional e os snapshots do pedido.
///
/// Ordem dos passos (e o motivo de cada posicao):
/// 1. carrinho, endereco e precos — falhar cedo, antes de qualquer I/O externo;
/// 2. recotacao de frete — I/O de rede, ainda FORA da transacao: cotacao presa por 30 s com
///    transacao aberta seguraria lock de estoque e derrubaria a concorrencia da loja;
/// 3. abre a transacao;
/// 4. cupom (UPDATE condicional atomico);
/// 5. pedido e itens, com todos os snapshots;
/// 6. reserva de estoque item a item, ja com o id do pedido para o ledger;
/// 7. pagamento e gateway. Gateway recusou, lanca: o rollback desfaz reserva, cupom e pedido;
/// 8. commit.
/// </summary>
public sealed class CheckoutService : ICheckoutService
{
    /// <summary>
    /// Prefixo do identificador de correlacao no gateway.
    ///
    /// Falha #3 do repo de referencia: la o order_nsu era "loja-{id}" sequencial, entao qualquer
    /// um enumerava pedidos alheios e forjava retorno de pagamento. Aqui e prefixo mais GUID.
    /// </summary>
    private const string PrefixoOrderNsu = "glo-";

    /// <summary>Rota publica para onde a InfinitePay devolve o navegador do cliente.</summary>
    private const string CaminhoRetornoPagamento = "api/v1/webhooks/pagamento/retorno";

    /// <summary>Rota publica de notificacao server-to-server.</summary>
    private const string CaminhoWebhookPagamento = "api/v1/webhooks/pagamento";

    /// <summary>Colisao de numero de pedido e disputa de indice unico, nao erro fatal.</summary>
    private const int TentativasNumeroPedido = 3;

    private readonly ICarrinhoRepository _carrinhos;
    private readonly IEnderecoRepository _enderecos;
    private readonly IProdutoVariacaoRepository _variacoes;
    private readonly IPedidoRepository _pedidos;
    private readonly IPagamentoRepository _pagamentos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IMidiaRepository _midias;
    private readonly IConfiguracaoLojaRepository _configuracoes;
    private readonly IFreteService _fretes;
    private readonly ICupomService _cupons;
    private readonly IEstoqueService _estoques;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;
    private readonly AppOptions _app;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        ICarrinhoRepository carrinhos,
        IEnderecoRepository enderecos,
        IProdutoVariacaoRepository variacoes,
        IPedidoRepository pedidos,
        IPagamentoRepository pagamentos,
        IUsuarioRepository usuarios,
        IMidiaRepository midias,
        IConfiguracaoLojaRepository configuracoes,
        IFreteService fretes,
        ICupomService cupons,
        IEstoqueService estoques,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IClock relogio,
        IOptions<AppOptions> app,
        ILogger<CheckoutService> logger)
    {
        _carrinhos = carrinhos ?? throw new ArgumentNullException(nameof(carrinhos));
        _enderecos = enderecos ?? throw new ArgumentNullException(nameof(enderecos));
        _variacoes = variacoes ?? throw new ArgumentNullException(nameof(variacoes));
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _pagamentos = pagamentos ?? throw new ArgumentNullException(nameof(pagamentos));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _midias = midias ?? throw new ArgumentNullException(nameof(midias));
        _configuracoes = configuracoes ?? throw new ArgumentNullException(nameof(configuracoes));
        _fretes = fretes ?? throw new ArgumentNullException(nameof(fretes));
        _cupons = cupons ?? throw new ArgumentNullException(nameof(cupons));
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _app = app?.Value ?? throw new ArgumentNullException(nameof(app));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CheckoutCriadoResponseDto> FinalizarAsync(
        string usuarioUuid,
        CheckoutRequestDto requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var usuario = await ObterUsuarioAsync(usuarioUuid, cancellationToken);

        var carrinho = await _carrinhos.ObterAbertoDoUsuarioAsync(usuario.Id, cancellationToken)
            ?? throw new BusinessValidationException("Seu carrinho esta vazio.");

        if (carrinho.Itens.Count == 0)
            throw new BusinessValidationException("Seu carrinho esta vazio.");

        // Posse conferida DENTRO da consulta: endereco de outra pessoa nao existe para este
        // usuario e vira 404. Carregar por id e comparar o dono depois vaza existencia.
        var endereco = await _enderecos.ObterDoUsuarioAsync(usuario.Id, requisicao.IdEndereco, cancellationToken)
            ?? throw new EntityNotFoundException("Endereco de entrega", requisicao.IdEndereco);

        ValidarEnderecoParaEtiqueta(endereco);

        var linhas = await MontarLinhasAsync(carrinho, cancellationToken);
        var subtotalCentavos = linhas.Sum(linha => linha.BrutoCentavos);

        await ValidarPedidoMinimoAsync(subtotalCentavos, cancellationToken);

        // Recotacao server-side (armadilha #9), FORA da transacao por ser I/O de rede. O
        // FreteService ja aplica a regra de frete gratis da loja e ja soma o prazo de manuseio;
        // ValorCentavos e o que se cobra, ValorCotadoCentavos e o que a transportadora pediu.
        var frete = await _fretes.RecotarServicoAsync(
            endereco.Cep,
            [.. linhas.Select(linha => new ItemCotacaoDto
            {
                IdVariacao = linha.Variacao.Id,
                Quantidade = linha.Quantidade
            })],
            requisicao.IdServicoFrete,
            cancellationToken);

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var cupom = await ConsumirCupomAsync(
                requisicao.CodigoCupom, usuario.Id, subtotalCentavos, frete.ValorCentavos, linhas, cancellationToken);

            var descontoItensCentavos = cupom?.DescontoProdutosCentavos ?? 0;
            var descontoFreteCentavos = cupom?.DescontoFreteCentavos ?? 0;

            DistribuirDesconto(linhas, descontoItensCentavos);

            var descontoCupomCentavos = descontoItensCentavos + descontoFreteCentavos;
            var freteLiquidoCentavos = Math.Max(0, frete.ValorCentavos - descontoFreteCentavos);
            var totalCentavos = linhas.Sum(linha => linha.TotalLinhaCentavos) + freteLiquidoCentavos;

            if (totalCentavos <= 0)
            {
                // Total zero nao gera link de pagamento e deixaria o pedido preso para sempre em
                // "aguardando pagamento". Cupom que zera o pedido e caso de negocio, nao de codigo.
                throw new BusinessValidationException(
                    "O total do pedido ficou zerado. Revise o cupom aplicado.");
            }

            var agora = _relogio.UtcNow;

            var pedido = await CriarPedidoAsync(
                usuario,
                endereco,
                linhas,
                frete,
                cupom,
                subtotalCentavos,
                descontoCupomCentavos,
                freteLiquidoCentavos,
                totalCentavos,
                requisicao.ObservacaoCliente,
                agora,
                cancellationToken);

            // Reserva SOFT item a item (armadilha #1), ja com o id do pedido para o ledger poder
            // responder depois por que o disponivel caiu. Falha aqui derruba a transacao inteira.
            await ReservarEstoqueAsync(linhas, pedido.Id, usuario.Id, cancellationToken);

            // Gerado AQUI e persistido antes de qualquer chamada ao gateway: e a chave de
            // correlacao do webhook e do redirect, e sem ela gravada nao ha como conferir depois.
            var orderNsu = PrefixoOrderNsu + Guid.NewGuid().ToString("N");

            var pagamento = new Pagamento
            {
                IdPedido = pedido.Id,
                Provedor = _gateway.Nome,
                Status = StatusPagamento.Pendente,
                ValorCentavos = totalCentavos,
                ProviderOrderId = orderNsu,
                DataCriacao = agora
            };

            await _pagamentos.AdicionarAsync(pagamento, cancellationToken);

            if (cupom is not null)
            {
                // O unico (id_cupom, id_pedido) so protege contra retentativa se esta escrita
                // commitar junto com o pedido — por isso ela vive aqui dentro.
                await _cupons.RegistrarUsoAsync(
                    cupom.IdCupom, usuario.Id, pedido.Id, cupom.DescontoTotalCentavos, cancellationToken);
            }

            await _pedidos.RegistrarHistoricoAsync(
                new PedidoHistorico
                {
                    IdPedido = pedido.Id,
                    StatusAnterior = null,
                    StatusNovo = StatusPedido.AguardandoPagamento,
                    IdUsuario = usuario.Id,
                    Observacao = "Pedido criado no checkout.",
                    DataAlteracao = agora
                },
                cancellationToken);

            carrinho.Status = StatusCarrinho.Convertido;
            carrinho.DataAlteracao = agora;
            _carrinhos.Atualizar(carrinho);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Ultimo passo antes do commit. Sem link nao existe pedido: lancar aqui desfaz
            // reserva de estoque, uso de cupom e o proprio pedido de uma vez so.
            var criado = await ChamarGatewayAsync(
                pedido, linhas, freteLiquidoCentavos, usuario, endereco, orderNsu, cancellationToken);

            pagamento.PaymentUrl = criado.UrlCheckout;
            pagamento.ProviderChargeId = criado.ProviderChargeId;
            pagamento.QrCodePix = criado.QrCodePix;
            pagamento.LinhaDigitavel = criado.LinhaDigitavel;
            pagamento.ExpiraEm = criado.ExpiraEmUtc;
            pagamento.RawUltimaResposta = criado.RawJson;

            _pagamentos.Atualizar(pagamento);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Checkout concluido. Pedido={Numero} Total={Total} OrderNsu={OrderNsu}",
                pedido.Numero,
                totalCentavos,
                orderNsu);

            return new CheckoutCriadoResponseDto
            {
                Numero = pedido.Numero,
                Uuid = pedido.Uuid,
                PaymentUrl = criado.UrlCheckout,
                QrCodePix = criado.QrCodePix,
                LinhaDigitavel = criado.LinhaDigitavel,
                ExpiraEm = criado.ExpiraEmUtc,
                SubtotalCentavos = subtotalCentavos,
                DescontoCupomCentavos = descontoCupomCentavos,
                FreteCentavos = freteLiquidoCentavos,
                TotalCentavos = totalCentavos
            };
        }
        catch
        {
            // Rollback explicito: o descarte do using tambem desfaria, mas deixar escrito e o que
            // impede alguem "otimizar" o using no futuro e transformar falha em pedido fantasma.
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CheckoutStatusResponseDto> ConsultarStatusAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(usuarioUuid, cancellationToken);

        var pedido = await _pedidos.ObterDoUsuarioAsync(usuario.Id, pedidoUuid, cancellationToken)
            ?? throw new EntityNotFoundException("Pedido", pedidoUuid);

        var statusPagamento = pedido.Pagamento?.Status ?? StatusPagamento.Pendente;

        var terminal = statusPagamento is not StatusPagamento.Pendente
            || pedido.Status is not StatusPedido.AguardandoPagamento;

        return new CheckoutStatusResponseDto
        {
            Uuid = pedido.Uuid,
            Numero = pedido.Numero,
            StatusPedido = pedido.Status.ToString(),
            StatusPagamento = statusPagamento.ToString(),
            Pago = statusPagamento == StatusPagamento.Aprovado,
            Terminal = terminal,
            // O link so serve enquanto ha o que pagar; devolve-lo depois convida o cliente a
            // pagar de novo um pedido ja quitado.
            PaymentUrl = statusPagamento == StatusPagamento.Pendente ? pedido.Pagamento?.PaymentUrl : null,
            ExpiraEm = pedido.Pagamento?.ExpiraEm
        };
    }

    // ------------------------------------------------------------------
    // Passos
    // ------------------------------------------------------------------

    private async Task<Usuario> ObterUsuarioAsync(string usuarioUuid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioUuid))
            throw new UnauthorizedAccessException("Token sem identificacao de usuario.");

        return await _usuarios.ObterPorUuidAsync(usuarioUuid, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuario do token nao existe mais.");
    }

    /// <summary>
    /// Armadilha #6 do modelo: sem CPF do destinatario a etiqueta falha DEPOIS do cliente ja ter
    /// pago. Bairro vazio e recusa direta do Melhor Envio. Os dois sao barrados aqui, antes de
    /// existir cobranca.
    /// </summary>
    private static void ValidarEnderecoParaEtiqueta(Endereco endereco)
    {
        var erros = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!DocumentoHelper.CpfValido(endereco.DocumentoDestinatario))
        {
            erros["documentoDestinatario"] =
                ["Informe um CPF valido para o destinatario: a transportadora exige documento na etiqueta."];
        }

        if (string.IsNullOrWhiteSpace(endereco.Bairro))
            erros["bairro"] = ["O bairro e obrigatorio para gerar a etiqueta."];

        if (!CepHelper.Valido(endereco.Cep))
            erros["cep"] = ["CEP invalido."];

        if (erros.Count > 0)
        {
            throw new BusinessValidationException(
                "Complete o endereco de entrega antes de finalizar a compra.", erros);
        }
    }

    private async Task ValidarPedidoMinimoAsync(int subtotalCentavos, CancellationToken cancellationToken)
    {
        var configuracao = await _configuracoes.ObterAsync(cancellationToken);
        var minimo = configuracao?.PedidoMinimoCentavos ?? 0;

        if (minimo > 0 && subtotalCentavos < minimo)
            throw new BusinessValidationException($"O pedido minimo desta loja e de {FormatarReais(minimo)}.");
    }

    /// <summary>
    /// Monta as linhas do pedido e revalida preco contra o snapshot do carrinho.
    ///
    /// Divergencia NAO e silenciada: cobrar o preco novo sem avisar e problema de consumidor, e
    /// cobrar o preco antigo e prejuizo. O cliente revisa o carrinho e refaz.
    /// </summary>
    private async Task<List<LinhaCheckout>> MontarLinhasAsync(
        CarrinhoEntity carrinho,
        CancellationToken cancellationToken)
    {
        var ids = carrinho.Itens.Select(item => item.IdVariacao).Distinct().ToArray();

        var variacoes = (await _variacoes.ObterParaCheckoutAsync(ids, cancellationToken))
            .ToDictionary(variacao => variacao.Id);

        var imagens = await ObterImagensDeCapaAsync(variacoes.Values, cancellationToken);

        var indisponiveis = new List<string>();
        var divergencias = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var linhas = new List<LinhaCheckout>(carrinho.Itens.Count);

        foreach (var item in carrinho.Itens.OrderBy(item => item.Id))
        {
            if (!variacoes.TryGetValue(item.IdVariacao, out var variacao) || !variacao.Ativo)
            {
                indisponiveis.Add("Um item do carrinho nao esta mais disponivel.");
                continue;
            }

            if (item.Quantidade <= 0)
            {
                indisponiveis.Add($"{Descrever(variacao)} esta com quantidade invalida.");
                continue;
            }

            var precoAtual = variacao.PrecoEfetivoCentavos;

            if (precoAtual <= 0)
            {
                indisponiveis.Add($"{Descrever(variacao)} esta sem preco cadastrado.");
                continue;
            }

            if (precoAtual != item.PrecoUnitarioSnapshotCentavos)
            {
                divergencias[$"item.{variacao.Sku}"] =
                [
                    $"O preco de {Descrever(variacao)} mudou de " +
                    $"{FormatarReais(item.PrecoUnitarioSnapshotCentavos)} para {FormatarReais(precoAtual)}."
                ];
                continue;
            }

            linhas.Add(new LinhaCheckout
            {
                Item = item,
                Variacao = variacao,
                PrecoUnitarioCentavos = precoAtual,
                Descricao = Descrever(variacao),
                ImagemUrl = imagens.GetValueOrDefault(variacao.IdProduto)
            });
        }

        if (divergencias.Count > 0)
        {
            throw new BusinessValidationException(
                "Precos atualizados. Revise o carrinho antes de finalizar.", divergencias);
        }

        if (indisponiveis.Count > 0)
            throw new BusinessValidationException(string.Join(" ", indisponiveis));

        if (linhas.Count == 0)
            throw new BusinessValidationException("Seu carrinho esta vazio.");

        return linhas;
    }

    /// <summary>
    /// Capa por produto, para congelar a foto no item do pedido.
    ///
    /// Uma consulta por PRODUTO distinto (nao por item): carrinho de moda tem varias variacoes do
    /// mesmo modelo, e consultar por item multiplicaria a mesma leitura.
    /// </summary>
    private async Task<Dictionary<int, string?>> ObterImagensDeCapaAsync(
        IEnumerable<ProdutoVariacao> variacoes,
        CancellationToken cancellationToken)
    {
        var imagens = new Dictionary<int, string?>();

        foreach (var idProduto in variacoes.Select(variacao => variacao.IdProduto).Distinct())
        {
            var galeria = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);

            // A galeria ja vem ordenada por EhCapa e depois por Ordem explicita: a primeira e a capa.
            imagens[idProduto] = galeria.FirstOrDefault()?.Midia?.Url;
        }

        return imagens;
    }

    /// <summary>
    /// Valida e CONSOME o cupom atomicamente, delegando a regra inteira ao CupomService.
    ///
    /// Consumir e nao apenas validar: o UPDATE condicional (WHERE usos_atuais menor que o maximo)
    /// e o que impede dois checkouts simultaneos levarem o ultimo uso do cupom dos "primeiros
    /// cem" (armadilha #8). Estamos dentro da transacao, entao um aborto posterior devolve o uso
    /// pelo rollback — sem precisar de compensacao manual.
    /// </summary>
    private async Task<CupomAplicadoDto?> ConsumirCupomAsync(
        string? codigo,
        int idUsuario,
        int subtotalCentavos,
        int freteCentavos,
        IReadOnlyList<LinhaCheckout> linhas,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;

        var resultado = await _cupons.ConsumirAsync(
            new CupomValidacaoRequest
            {
                Codigo = codigo,
                IdUsuario = idUsuario,
                SubtotalCentavos = subtotalCentavos,
                FreteCentavos = freteCentavos,
                Itens =
                [
                    .. linhas.Select(linha => new CupomItemContexto
                    {
                        IdProduto = linha.Variacao.IdProduto,
                        Quantidade = linha.Quantidade,
                        TotalLinhaCentavos = linha.BrutoCentavos
                    })
                ]
            },
            cancellationToken);

        // Cupom recusado e caminho previsivel para o CupomService, mas na fronteira do checkout
        // ele tem de virar 400 e abortar: nao da para cobrar um total diferente do que a tela
        // mostrou com o cupom aplicado.
        return resultado.ValorOuLancar();
    }

    /// <summary>
    /// Espalha o desconto pelas linhas proporcionalmente ao valor de cada uma, e joga o resto da
    /// divisao na maior linha.
    ///
    /// Por que distribuir em vez de guardar so o total: o gateway recebe uma linha por item e a
    /// soma das linhas TEM que fechar com o total cobrado — senao a conferencia de valor no
    /// webhook nunca bate e todo pedido cai em revisao manual.
    /// </summary>
    private static void DistribuirDesconto(IReadOnlyList<LinhaCheckout> linhas, int descontoTotalCentavos)
    {
        if (descontoTotalCentavos <= 0)
            return;

        var bruto = linhas.Sum(linha => linha.BrutoCentavos);

        if (bruto <= 0)
            return;

        var distribuido = 0;

        foreach (var linha in linhas)
        {
            // Desconto POR UNIDADE, porque e assim que ele e gravado no item do pedido.
            var parteLinha = (int)((long)descontoTotalCentavos * linha.BrutoCentavos / bruto);
            var porUnidade = parteLinha / linha.Quantidade;

            linha.DescontoUnitarioCentavos = Math.Min(porUnidade, linha.PrecoUnitarioCentavos);
            distribuido += linha.DescontoUnitarioCentavos * linha.Quantidade;
        }

        var resto = descontoTotalCentavos - distribuido;

        if (resto <= 0)
            return;

        // O resto (centavos que a divisao inteira deixou) vai para a linha de maior valor, que e
        // a que suporta o ajuste sem risco de ficar com preco negativo.
        var maior = linhas.OrderByDescending(linha => linha.BrutoCentavos).First();
        var espaco = (maior.PrecoUnitarioCentavos - maior.DescontoUnitarioCentavos) * maior.Quantidade;

        var ajustePorUnidade = Math.Min(resto, espaco) / maior.Quantidade;

        maior.DescontoUnitarioCentavos += ajustePorUnidade;
    }

    /// <summary>
    /// Reserva item a item pelo EstoqueService, que ja faz o UPDATE condicional atomico e grava o
    /// ledger. Todas as falhas sao coletadas antes de lancar: o cliente ve de uma vez quais pecas
    /// esgotaram em vez de descobrir uma por tentativa. O que ja foi reservado nesta iteracao
    /// volta pelo rollback da transacao.
    /// </summary>
    private async Task ReservarEstoqueAsync(
        IReadOnlyList<LinhaCheckout> linhas,
        int idPedido,
        int idUsuario,
        CancellationToken cancellationToken)
    {
        var esgotados = new List<string>();

        foreach (var linha in linhas)
        {
            var reserva = await _estoques.ReservarAsync(
                linha.Variacao.Id, linha.Quantidade, idPedido, idUsuario, cancellationToken);

            if (reserva.Falhou)
                esgotados.Add(reserva.Erro ?? $"{linha.Descricao} esgotou.");
        }

        if (esgotados.Count == 0)
            return;

        var detalhe = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["itens"] = [.. esgotados]
        };

        throw new BusinessValidationException(
            esgotados.Count == 1
                ? esgotados[0]
                : "Alguns itens esgotaram enquanto voce finalizava a compra.",
            detalhe);
    }

    private async Task<Pedido> CriarPedidoAsync(
        Usuario usuario,
        Endereco endereco,
        IReadOnlyList<LinhaCheckout> linhas,
        OpcaoFreteResponseDto frete,
        CupomAplicadoDto? cupom,
        int subtotalCentavos,
        int descontoCupomCentavos,
        int freteCentavos,
        int totalCentavos,
        string? observacao,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        var pedido = new Pedido
        {
            Numero = await _pedidos.GerarProximoNumeroAsync(agora.Year, cancellationToken),
            Uuid = Guid.NewGuid().ToString(),
            IdUsuario = usuario.Id,
            Status = StatusPedido.AguardandoPagamento,
            SubtotalCentavos = subtotalCentavos,
            DescontoCupomCentavos = descontoCupomCentavos,
            FreteCentavos = freteCentavos,
            TotalCentavos = totalCentavos,
            IdCupom = cupom?.IdCupom,
            CodigoCupomSnapshot = cupom?.Codigo,
            IdServicoFrete = frete.IdServico,
            TransportadoraFrete = frete.Transportadora,
            ServicoFrete = frete.Servico,
            // Ja vem com o manuseio somado pelo FreteService: exibir so o prazo da transportadora
            // e prometer entrega que a expedicao nao cumpre.
            PrazoFreteDias = frete.PrazoDias,
            ObservacaoCliente = observacao,
            PesoTotalGramas = linhas.Sum(linha => linha.Variacao.PesoGramas * linha.Quantidade),
            DataCriacao = agora,
            // Copia, nao referencia: o cliente vai editar ou apagar o endereco, e o pedido de
            // seis meses atras nao pode passar a dizer que foi entregue em outro lugar.
            EnderecoEntrega = new PedidoEnderecoSnapshot
            {
                Destinatario = endereco.Destinatario,
                DocumentoDestinatario = DocumentoHelper.SomenteDigitos(endereco.DocumentoDestinatario),
                TelefoneContato = TelefoneHelper.SomenteDigitos(endereco.TelefoneContato),
                Cep = CepHelper.SomenteDigitos(endereco.Cep),
                Logradouro = endereco.Logradouro,
                Numero = endereco.Numero,
                Complemento = endereco.Complemento,
                Bairro = endereco.Bairro,
                Cidade = endereco.Cidade,
                Uf = endereco.Uf.ToUpperInvariant(),
                Pais = endereco.Pais
            }
        };

        foreach (var linha in linhas)
        {
            pedido.Itens.Add(new PedidoItem
            {
                IdVariacao = linha.Variacao.Id,
                IdProduto = linha.Variacao.IdProduto,
                SkuSnapshot = linha.Variacao.Sku,
                NomeProdutoSnapshot = linha.Variacao.Produto?.Nome ?? linha.Variacao.Sku,
                TamanhoSnapshot = linha.Variacao.Tamanho?.Codigo ?? "-",
                CorSnapshot = linha.Variacao.Cor?.Nome ?? "-",
                ImagemUrlSnapshot = linha.ImagemUrl,
                Quantidade = linha.Quantidade,
                PrecoUnitarioCentavos = linha.PrecoUnitarioCentavos,
                DescontoUnitarioCentavos = linha.DescontoUnitarioCentavos,
                PesoGramasSnapshot = linha.Variacao.PesoGramas,
                TotalLinhaCentavos = linha.TotalLinhaCentavos
            });
        }

        await _pedidos.AdicionarAsync(pedido, cancellationToken);

        // O numero e disputado por indice unico. Salvar aqui isola a colisao: se dois checkouts
        // simultaneos geraram o mesmo sequencial, um deles refaz o numero em vez de derrubar a
        // transacao inteira ja com reserva e pagamento dentro.
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return pedido;
            }
            catch (Exception excecao) when (tentativa < TentativasNumeroPedido
                                            && excecao is not OperationCanceledException)
            {
                _logger.LogWarning(
                    excecao,
                    "Colisao ao gravar o pedido {Numero}. Regerando o numero (tentativa {Tentativa}).",
                    pedido.Numero,
                    tentativa);

                pedido.Numero = await _pedidos.GerarProximoNumeroAsync(agora.Year, cancellationToken);
            }
        }
    }

    private async Task<CheckoutCriadoInfo> ChamarGatewayAsync(
        Pedido pedido,
        IReadOnlyList<LinhaCheckout> linhas,
        int freteCentavos,
        Usuario usuario,
        Endereco endereco,
        string orderNsu,
        CancellationToken cancellationToken)
    {
        var itens = new List<CheckoutItemInfo>(linhas.Count + 1);

        foreach (var linha in linhas)
        {
            itens.Add(new CheckoutItemInfo
            {
                Descricao = linha.Descricao,
                Quantidade = linha.Quantidade,
                // Preco LIQUIDO: o gateway nao tem linha de desconto, entao o desconto ja vem
                // embutido no valor unitario. E o que faz a soma fechar com o total do pedido.
                PrecoUnitarioCentavos = linha.PrecoUnitarioCentavos - linha.DescontoUnitarioCentavos
            });
        }

        if (freteCentavos > 0)
        {
            // Frete como LINHA PROPRIA e valor flat, fora de qualquer multiplicador de metodo de
            // pagamento — heranca do adaptador de checkout do repo de referencia.
            itens.Add(new CheckoutItemInfo
            {
                Descricao = $"Frete - {pedido.ServicoFrete ?? "envio"}",
                Quantidade = 1,
                PrecoUnitarioCentavos = freteCentavos
            });
        }

        var requisicao = new CheckoutRequisicaoInfo
        {
            OrderNsu = orderNsu,
            Itens = itens,
            UrlRetorno = _app.UrlApi(CaminhoRetornoPagamento),
            UrlWebhook = _app.UrlApi(CaminhoWebhookPagamento),
            TotalCentavos = pedido.TotalCentavos,
            Cliente = new CheckoutClienteInfo
            {
                Nome = usuario.NomeCompleto ?? endereco.Destinatario,
                Email = usuario.Email,
                Telefone = TelefoneHelper.SomenteDigitos(endereco.TelefoneContato)
            }
        };

        var criado = await _gateway.CriarCheckoutAsync(requisicao, cancellationToken);

        if (!criado.Sucesso || string.IsNullOrWhiteSpace(criado.UrlCheckout))
        {
            _logger.LogError(
                "Gateway recusou a cobranca do pedido {Numero}: {Erro}",
                pedido.Numero,
                criado.Erro);

            // Lanca para o rollback desfazer pedido, reserva e cupom. Pedido comitado sem link de
            // pagamento seria estoque preso sem ninguem para pagar.
            throw new BusinessValidationException(
                "Nao foi possivel iniciar o pagamento agora. Tente novamente em instantes.");
        }

        return criado;
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static string Descrever(ProdutoVariacao variacao)
    {
        var nome = variacao.Produto?.Nome ?? variacao.Sku;
        var tamanho = variacao.Tamanho?.Codigo;
        var cor = variacao.Cor?.Nome;

        if (string.IsNullOrWhiteSpace(tamanho) && string.IsNullOrWhiteSpace(cor))
            return nome;

        return $"{nome} - {tamanho ?? "-"} / {cor ?? "-"}";
    }

    /// <summary>
    /// Formatacao de exibicao a partir de centavos. Cultura fixa de proposito: a mensagem vai
    /// para o cliente e nao pode depender da cultura do processo do container.
    /// </summary>
    private static string FormatarReais(int centavos) =>
        $"R$ {(centavos / 100m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";

    /// <summary>
    /// Linha em construcao. Classe mutavel e nao record porque o desconto e atribuido depois, na
    /// distribuicao — e um record com setter publico so mascararia isso.
    /// </summary>
    private sealed class LinhaCheckout
    {
        public required CarrinhoItemEntity Item { get; init; }

        public required ProdutoVariacao Variacao { get; init; }

        public required int PrecoUnitarioCentavos { get; init; }

        public required string Descricao { get; init; }

        public string? ImagemUrl { get; init; }

        public int DescontoUnitarioCentavos { get; set; }

        public int Quantidade => Item.Quantidade;

        /// <summary>Valor da linha antes do cupom.</summary>
        public int BrutoCentavos => Quantidade * PrecoUnitarioCentavos;

        /// <summary>Valor efetivamente cobrado na linha.</summary>
        public int TotalLinhaCentavos => Quantidade * (PrecoUnitarioCentavos - DescontoUnitarioCentavos);
    }
}
