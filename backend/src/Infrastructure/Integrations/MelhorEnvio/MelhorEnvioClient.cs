using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Application.Common;
using Glorific.Application.Exceptions;
using Glorific.Application.Models.MelhorEnvio;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Integrations.MelhorEnvio;

/// <summary>
/// Adaptador do microservico integracaoMelhorEnvio (NAO da API do Melhor Envio direta: o OAuth,
/// a renovacao do token e o passthrough do corpo cru ficam la).
///
/// Este e o unico lugar do sistema que conhece o contrato do parceiro. Tres decisoes moram aqui:
///
/// 1. TRADUCAO DE UNIDADE NA FRONTEIRA. Dentro do sistema dinheiro e centavos inteiro e peso e
///    grama inteiro; o parceiro fala reais e quilos decimais, e em dois campos exige STRING
///    ("quantity", "unitary_value"). A conversao acontece nesta classe e em nenhuma outra.
///
/// 2. TODA FALHA VIRA MelhorEnvioApiException. Nenhum HttpResponseMessage, JsonElement ou status
///    code cru cruza a porta. Os servicos decidem por EhErroCliente / EhFalhaComunicacao /
///    EhContaNaoConectada, porque status code aqui e HTTP do ME repassado: um 404 significa
///    "conta nao conectada" (problema operacional nosso), nao "recurso inexistente".
///
/// 3. O CORPO CRU E PRESERVADO em RawJson de cada resultado, para gravar em
///    envios.raw_ultima_resposta (jsonb). Quando o parceiro muda o formato sem avisar, e essa
///    coluna que permite reconstruir o que aconteceu num pedido especifico.
///
/// O accountId nao aparece na porta de proposito: e multi-tenancy do parceiro, detalhe deste
/// adaptador, e vai como query em TODAS as rotas.
/// </summary>
public sealed class MelhorEnvioClient : IMelhorEnvioClient
{
    private const string HeaderApiKey = "X-Api-Key";

    // Rotas do MICROSERVICO (nao as do Melhor Envio). Constantes para o typo virar erro de
    // compilacao: caminho errado no HttpClient devolve 404 do host certo, que e o erro mais
    // caro de diagnosticar nesta integracao.
    private const string RotaCotacao = "/api/shipment/calculate";

    private const string RotaCarrinho = "/api/cart";
    private const string RotaCompra = "/api/cart/checkout";
    private const string RotaGerar = "/api/labels/generate";
    private const string RotaImprimir = "/api/labels/print";
    private const string RotaRastreio = "/api/shipment/tracking";
    private const string RotaCancelar = "/api/shipment/cancel";
    private const string RotaSaldo = "/api/me/balance";
    private const string RotaStatusConta = "/api/auth/status";

    private readonly HttpClient _http;
    private readonly MelhorEnvioOptions _opcoes;
    private readonly ILogger<MelhorEnvioClient> _logger;

    public MelhorEnvioClient(
        HttpClient http,
        IOptions<MelhorEnvioOptions> opcoes,
        ILogger<MelhorEnvioClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _opcoes = opcoes?.Value ?? throw new ArgumentNullException(nameof(opcoes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // BaseAddress e Timeout normalmente ja vem do registro tipado no Program.cs. O fallback
        // existe para o cliente continuar utilizavel em teste, onde o HttpClient e montado a mao.
        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_opcoes.BaseUrl))
            _http.BaseAddress = new Uri(_opcoes.BaseUrl.TrimEnd('/'), UriKind.Absolute);

        if (string.IsNullOrWhiteSpace(_opcoes.ApiKey))
        {
            // Sem chave TODA rota do microservico responde 401 com corpo vazio. Avisar aqui, no
            // boot do escopo, e mais barato que descobrir no primeiro cliente que cotar frete.
            _logger.LogWarning(
                "MelhorEnvio:ApiKey nao configurada. Toda chamada ao servico de frete vai responder 401.");
        }
        else if (!_http.DefaultRequestHeaders.Contains(HeaderApiKey))
        {
            _http.DefaultRequestHeaders.Add(HeaderApiKey, _opcoes.ApiKey.Trim());
        }
    }

    // ------------------------------------------------------------------
    // Cotacao
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<CotacaoFreteResultado>> CotarFreteAsync(
        CotacaoFreteRequisicao requisicao,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var temVolumes = requisicao.Volumes is { Count: > 0 };
        var temProdutos = requisicao.Produtos is { Count: > 0 };

        // O microservico devolve 400 quando os dois vem juntos e quando nenhum vem. Barrar aqui
        // evita gastar uma ida de rede de 2 a 5 s para receber um erro que ja era conhecido.
        if (temVolumes == temProdutos)
            throw new BusinessValidationException(
                "Cotacao invalida: informe produtos OU volumes, nunca os dois nem nenhum.");

        var corpo = new CotacaoPayload
        {
            From = new CepPayload { PostalCode = requisicao.CepOrigem },
            To = new CepPayload { PostalCode = requisicao.CepDestino },
            Products = temProdutos
                ? requisicao.Produtos.Select(p => new ProdutoCotacaoPayload
                {
                    Id = p.Id,
                    Width = p.LarguraCm,
                    Height = p.AlturaCm,
                    Length = p.ComprimentoCm,
                    Weight = p.PesoKg,
                    InsuranceValue = FreteConversoes.CentavosParaReais(p.ValorSeguradoCentavos),
                    Quantity = p.Quantidade <= 0 ? 1 : p.Quantidade
                }).ToArray()
                : null,
            Volumes = temVolumes
                ? requisicao.Volumes!.Select(v => new VolumeCotacaoPayload
                {
                    Width = v.LarguraCm,
                    Height = v.AlturaCm,
                    Length = v.ComprimentoCm,
                    Weight = v.PesoKg,
                    Insurance = FreteConversoes.CentavosParaReais(v.ValorSeguradoCentavos)
                }).ToArray()
                : null,
            Options = new OpcoesCotacaoPayload
            {
                Receipt = requisicao.AvisoRecebimento,
                OwnHand = requisicao.MaoPropria
            },
            Services = requisicao.Servicos is { Count: > 0 }
                ? string.Join(',', requisicao.Servicos)
                : null
        };

        var resposta = await EnviarAsync(HttpMethod.Post, RotaCotacao, corpo, "cotar o frete", ct);

        var resultados = new List<CotacaoFreteResultado>();

        foreach (var item in MelhorEnvioJson.ComoLista(resposta.Raiz))
        {
            var idServico = MelhorEnvioJson.Inteiro(item, "id");

            // Linha sem id nao serve para nada: e o id do servico que vai em "service" no
            // POST /api/cart. Descartar em silencio e melhor que carregar uma opcao inescolhivel.
            if (idServico is null or <= 0)
                continue;

            MelhorEnvioJson.TentarObter(item, "company", out var empresa);

            var precoCustomizado = MelhorEnvioJson.Decimal(item, "custom_price");
            var precoTabela = MelhorEnvioJson.Decimal(item, "price");

            resultados.Add(new CotacaoFreteResultado
            {
                IdServico = idServico.Value,
                NomeServico = MelhorEnvioJson.Texto(item, "name"),
                NomeTransportadora = empresa.ValueKind == JsonValueKind.Object
                    ? MelhorEnvioJson.Texto(empresa, "name")
                    : null,
                LogoTransportadora = empresa.ValueKind == JsonValueKind.Object
                    ? MelhorEnvioJson.Texto(empresa, "picture")
                    : null,

                // custom_price e o preco COM o desconto da conta aplicado — e o que o ME vai
                // debitar da carteira, e portanto o unico numero honesto para cobrar do cliente.
                PrecoCentavos = FreteConversoes.ReaisParaCentavos(precoCustomizado ?? precoTabela ?? 0m),
                PrecoTabelaCentavos = FreteConversoes.ReaisParaCentavos(precoTabela),
                DescontoCentavos = FreteConversoes.ReaisParaCentavos(MelhorEnvioJson.Decimal(item, "discount")),
                PrazoDias = MelhorEnvioJson.Inteiro(item, "custom_delivery_time")
                            ?? MelhorEnvioJson.Inteiro(item, "delivery_time"),

                // Indisponivel chega COM "error" preenchido em vez de sumir da lista. Quem
                // decide entre descartar (vitrine) e explicar (checkout) e o servico.
                Erro = MelhorEnvioJson.MensagemErro(item)
            });
        }

        return resultados;
    }

    // ------------------------------------------------------------------
    // Carrinho e compra da etiqueta
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<CarrinhoEnvioResultado> InserirNoCarrinhoAsync(
        CarrinhoEnvioRequisicao requisicao,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var corpo = new CarrinhoPayload
        {
            Service = requisicao.IdServico,
            Agency = requisicao.IdAgencia,
            From = ParaPayload(requisicao.Remetente),
            To = ParaPayload(requisicao.Destinatario),
            Products = requisicao.Produtos.Select(p => new ProdutoDeclaradoPayload
            {
                Name = p.Nome,
                // String, nao numero: e o contrato do parceiro, verificado nos testes dele.
                Quantity = p.Quantidade.ToString(CultureInfo.InvariantCulture),
                UnitaryValue = FreteConversoes.CentavosParaTexto(p.ValorUnitarioCentavos),
                Weight = p.PesoKg
            }).ToArray(),
            Volumes = requisicao.Volumes.Select(v => new VolumePayload
            {
                Height = v.AlturaCm,
                Width = v.LarguraCm,
                Length = v.ComprimentoCm,
                Weight = v.PesoKg
            }).ToArray(),
            Options = new OpcoesCarrinhoPayload
            {
                Platform = requisicao.Opcoes.Plataforma,
                InsuranceValue = FreteConversoes.CentavosParaReais(requisicao.Opcoes.ValorSeguradoCentavos),
                Receipt = requisicao.Opcoes.AvisoRecebimento,
                OwnHand = requisicao.Opcoes.MaoPropria,
                Reverse = requisicao.Opcoes.Reversa,
                NonCommercial = requisicao.Opcoes.NaoComercial,
                Tags = requisicao.Opcoes.Tags is { Count: > 0 }
                    ? requisicao.Opcoes.Tags.Select(t => new TagPayload { Tag = t.Tag, Url = t.Url }).ToArray()
                    : null,

                // invoice inteiro fora do payload quando nao ha nota: enviar o objeto com key
                // vazia faz o ME tratar como declaracao de conteudo invalida.
                Invoice = string.IsNullOrWhiteSpace(requisicao.Opcoes.ChaveNfe)
                    ? null
                    : new NotaFiscalPayload
                    {
                        Key = requisicao.Opcoes.ChaveNfe,
                        XmlContent = requisicao.Opcoes.XmlNfe
                    },
                Dce = string.IsNullOrWhiteSpace(requisicao.Opcoes.ChaveDce)
                    ? null
                    : new ChaveDcePayload { Key = requisicao.Opcoes.ChaveDce }
            }
        };

        var resposta = await EnviarAsync(HttpMethod.Post, RotaCarrinho, corpo, "reservar a etiqueta", ct);

        var meOrderId = MelhorEnvioJson.Texto(resposta.Raiz, "id");

        // Sem o uuid da etiqueta o fluxo inteiro para: e a chave de compra, geracao, impressao,
        // rastreio e cancelamento. Falhar alto aqui e melhor que gravar um envio sem chave e
        // descobrir no passo seguinte, com a etiqueta ja reservada no parceiro.
        if (string.IsNullOrWhiteSpace(meOrderId))
            throw new MelhorEnvioApiException(
                "O servico de frete aceitou a etiqueta mas nao devolveu o identificador dela.",
                statusCode: 502,
                corpoBruto: resposta.Bruto);

        return new CarrinhoEnvioResultado
        {
            MeOrderId = meOrderId,
            Protocolo = MelhorEnvioJson.Texto(resposta.Raiz, "protocol"),
            Status = MelhorEnvioJson.Texto(resposta.Raiz, "status"),
            PrecoCentavos = FreteConversoes.ReaisParaCentavos(MelhorEnvioJson.Decimal(resposta.Raiz, "price")),
            IdServico = MelhorEnvioJson.Inteiro(resposta.Raiz, "service_id"),
            RawJson = resposta.Bruto
        };
    }

    /// <inheritdoc />
    public async Task<CompraEtiquetaResultado> ComprarAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default)
    {
        var ids = NormalizarIds(meOrderIds, "comprar");

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            RotaCompra,
            new EtiquetasPayload { Orders = ids },
            "comprar a etiqueta",
            ct);

        // O ME embrulha tudo em "purchase". Quando o envelope nao vem, a resposta veio de um
        // formato que nao conhecemos e tratar como sucesso gravaria um envio comprado que nao foi.
        if (!MelhorEnvioJson.TentarObter(resposta.Raiz, "purchase", out var compra))
            throw new MelhorEnvioApiException(
                "O servico de frete respondeu a compra da etiqueta em formato inesperado.",
                statusCode: 502,
                corpoBruto: resposta.Bruto);

        var valores = new Dictionary<string, int>(StringComparer.Ordinal);

        if (MelhorEnvioJson.TentarObter(compra, "orders", out var etiquetas)
            && etiquetas.ValueKind == JsonValueKind.Array)
        {
            foreach (var etiqueta in etiquetas.EnumerateArray())
            {
                var id = MelhorEnvioJson.Texto(etiqueta, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var preco = MelhorEnvioJson.Decimal(etiqueta, "price")
                            ?? MelhorEnvioJson.Decimal(etiqueta, "subtotal");

                if (preco is not null)
                    valores[id] = FreteConversoes.ReaisParaCentavos(preco.Value);
            }
        }

        return new CompraEtiquetaResultado
        {
            Sucesso = true,
            IdCompra = MelhorEnvioJson.Texto(compra, "id"),
            Protocolo = MelhorEnvioJson.Texto(compra, "protocol"),
            Status = MelhorEnvioJson.Texto(compra, "status"),
            TotalCentavos = FreteConversoes.ReaisParaCentavos(MelhorEnvioJson.Decimal(compra, "total")),
            ValoresPorEtiqueta = valores,
            Mensagem = MelhorEnvioJson.Texto(compra, "message"),
            RawJson = resposta.Bruto
        };
    }

    /// <inheritdoc />
    public async Task<GeracaoEtiquetaResultado> GerarEtiquetaAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default)
    {
        var ids = NormalizarIds(meOrderIds, "gerar");

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            RotaGerar,
            new EtiquetasPayload { Orders = ids },
            "gerar a etiqueta",
            ct);

        var itens = new Dictionary<string, GeracaoEtiquetaItem>(StringComparer.Ordinal);

        if (resposta.Raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var propriedade in resposta.Raiz.EnumerateObject())
            {
                if (propriedade.Value.ValueKind != JsonValueKind.Object)
                    continue;

                itens[propriedade.Name] = new GeracaoEtiquetaItem
                {
                    // "status" aqui e BOOLEANO, e nao texto como em todo o resto da API do ME.
                    Sucesso = MelhorEnvioJson.Booleano(propriedade.Value, "status") ?? false,
                    Mensagem = MelhorEnvioJson.Texto(propriedade.Value, "message")
                };
            }
        }

        return new GeracaoEtiquetaResultado
        {
            Itens = itens,
            RawJson = resposta.Bruto
        };
    }

    /// <inheritdoc />
    public async Task<ImpressaoEtiquetaResultado> ImprimirEtiquetaAsync(
        IReadOnlyList<string> meOrderIds,
        ModoImpressaoEtiqueta modo = ModoImpressaoEtiqueta.Privado,
        CancellationToken ct = default)
    {
        var ids = NormalizarIds(meOrderIds, "imprimir");

        var corpo = new ImpressaoPayload
        {
            Orders = ids,

            // null some do payload e o link nasce privado. "public" gera URL que qualquer
            // pessoa abre — so no botao do admin, nunca no caminho automatico do worker.
            Mode = modo == ModoImpressaoEtiqueta.Publico ? "public" : null
        };

        var resposta = await EnviarAsync(HttpMethod.Post, RotaImprimir, corpo, "imprimir a etiqueta", ct);

        var url = MelhorEnvioJson.Texto(resposta.Raiz, "url");

        if (string.IsNullOrWhiteSpace(url))
            throw new MelhorEnvioApiException(
                "O servico de frete nao devolveu o link da etiqueta.",
                statusCode: 502,
                corpoBruto: resposta.Bruto);

        return new ImpressaoEtiquetaResultado { Url = url, RawJson = resposta.Bruto };
    }

    // ------------------------------------------------------------------
    // Rastreio e cancelamento
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<RastreioResultado>> RastrearAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default)
    {
        var ids = NormalizarIds(meOrderIds, "rastrear");

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            RotaRastreio,
            new EtiquetasPayload { Orders = ids },
            "rastrear a etiqueta",
            ct);

        var resultados = new List<RastreioResultado>();

        if (resposta.Raiz.ValueKind != JsonValueKind.Object)
            return resultados;

        foreach (var propriedade in resposta.Raiz.EnumerateObject())
        {
            if (propriedade.Value.ValueKind != JsonValueKind.Object)
                continue;

            var item = propriedade.Value;
            var statusOriginal = MelhorEnvioJson.Texto(item, "status");

            resultados.Add(new RastreioResultado
            {
                // A chave do mapa e o meOrderId; "id" repetido dentro do objeto e so redundancia
                // do parceiro e ja chegou ausente.
                MeOrderId = MelhorEnvioJson.Texto(item, "id") ?? propriedade.Name,
                Protocolo = MelhorEnvioJson.Texto(item, "protocol"),
                StatusOriginal = statusOriginal,
                StatusEquivalente = TraduzirStatus(statusOriginal),
                CodigoRastreio = MelhorEnvioJson.Texto(item, "tracking"),
                UrlRastreio = MelhorEnvioJson.Texto(item, "melhorenvio_tracking"),
                PostadoEmUtc = MelhorEnvioJson.DataUtc(item, "posted_at"),
                EntregueEmUtc = MelhorEnvioJson.DataUtc(item, "delivered_at"),
                Eventos = ExtrairEventos(item),
                RawJson = item.GetRawText()
            });
        }

        return resultados;
    }

    /// <inheritdoc />
    public async Task<CancelamentoEtiquetaResultado> CancelarAsync(
        CancelamentoEtiquetaRequisicao requisicao,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        if (string.IsNullOrWhiteSpace(requisicao.MeOrderId))
            throw new BusinessValidationException("Cancelamento sem identificador de etiqueta.");

        var corpo = new CancelamentoPayload
        {
            Order = new CancelamentoOrdemPayload
            {
                Id = requisicao.MeOrderId,
                // Lista fechada do parceiro; "2" e o generico aceito em integracao.
                ReasonId = string.IsNullOrWhiteSpace(requisicao.MotivoId) ? "2" : requisicao.MotivoId,
                Description = requisicao.Descricao
            }
        };

        var resposta = await EnviarAsync(HttpMethod.Post, RotaCancelar, corpo, "cancelar a etiqueta", ct);

        var sucesso = true;
        string? mensagem = null;

        // Resposta esperada: mapa id -> { canceled: true|false, reason: ... }.
        if (MelhorEnvioJson.TentarObter(resposta.Raiz, requisicao.MeOrderId, out var item)
            && item.ValueKind == JsonValueKind.Object)
        {
            sucesso = MelhorEnvioJson.Booleano(item, "canceled")
                      ?? MelhorEnvioJson.Booleano(item, "cancelled")
                      ?? MelhorEnvioJson.Booleano(item, "status")
                      ?? true;

            mensagem = MelhorEnvioJson.Texto(item, "message")
                       ?? MelhorEnvioJson.Texto(item, "reason");
        }

        return new CancelamentoEtiquetaResultado
        {
            Sucesso = sucesso,
            Mensagem = mensagem,
            RawJson = resposta.Bruto
        };
    }

    // ------------------------------------------------------------------
    // Conta e carteira
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<SaldoMelhorEnvio> ConsultarSaldoAsync(CancellationToken ct = default)
    {
        var resposta = await EnviarAsync(HttpMethod.Get, RotaSaldo, corpo: null, "consultar o saldo", ct);

        var saldo = MelhorEnvioJson.Decimal(resposta.Raiz, "balance")
                    ?? MelhorEnvioJson.Decimal(resposta.Raiz, "value")
                    ?? 0m;

        return new SaldoMelhorEnvio
        {
            SaldoCentavos = FreteConversoes.ReaisParaCentavos(saldo),
            Moeda = MelhorEnvioJson.Texto(resposta.Raiz, "currency") ?? "BRL",
            RawJson = resposta.Bruto
        };
    }

    /// <inheritdoc />
    public async Task<StatusContaMelhorEnvio> VerificarStatusContaAsync(CancellationToken ct = default)
    {
        try
        {
            var resposta = await EnviarAsync(
                HttpMethod.Get, RotaStatusConta, corpo: null, "verificar a conta de frete", ct);

            // UNICO endpoint com contrato tipado do microservico: camelCase, e nao o snake_case
            // do passthrough. Ler com as opcoes erradas devolveria tudo zerado sem erro nenhum.
            var payload = JsonSerializer.Deserialize<StatusContaPayload>(
                resposta.Bruto, MelhorEnvioJson.RespostaMicroservico);

            return new StatusContaMelhorEnvio
            {
                Conectada = payload?.Connected ?? false,
                ContaId = payload?.AccountId ?? _opcoes.ContaId,
                TipoToken = payload?.TokenType,
                Escopo = payload?.Scope,
                ExpiraEmUtc = payload?.ExpiresAtUtc,
                ExpiraEmSegundos = payload?.ExpiresInSeconds,
                PrecisaRenovar = payload?.NeedsRefresh ?? false
            };
        }
        catch (MelhorEnvioApiException excecao) when (excecao.StatusCode == 404)
        {
            // Contrato do microservico: /api/auth/status nunca deveria dar 404, mas se der e
            // porque a conta nao existe. Isto e healthcheck: "desconectada" e uma resposta
            // valida, nao um erro que derruba o painel.
            _logger.LogWarning(
                "Conta '{ContaId}' do Melhor Envio respondeu 404 no status. Tratando como desconectada.",
                _opcoes.ContaId);

            return new StatusContaMelhorEnvio { Conectada = false, ContaId = _opcoes.ContaId };
        }
    }

    // ------------------------------------------------------------------
    // Transporte
    // ------------------------------------------------------------------

    /// <summary>
    /// Faz a chamada e devolve o corpo ja parseado MAIS o texto cru.
    ///
    /// O cru viaja junto porque envios.raw_ultima_resposta e jsonb: quando o parceiro muda o
    /// formato sem avisar, e essa coluna que permite reconstruir o que aconteceu num pedido.
    /// </summary>
    private async Task<(JsonElement Raiz, string Bruto)> EnviarAsync(
        HttpMethod metodo,
        string caminho,
        object? corpo,
        string operacao,
        CancellationToken ct)
    {
        var rota = ComAccountId(caminho);

        using var requisicao = new HttpRequestMessage(metodo, rota);

        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo, corpo.GetType(), options: MelhorEnvioJson.Envio);

        HttpResponseMessage resposta;

        try
        {
            resposta = await _http.SendAsync(requisicao, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (TaskCanceledException excecao) when (!ct.IsCancellationRequested)
        {
            // TaskCanceledException sem cancelamento do CHAMADOR e timeout do HttpClient. A
            // distincao importa: cancelamento do cliente nao e incidente, timeout do parceiro e.
            _logger.LogWarning(excecao, "Timeout ao {Operacao} no servico de frete ({Rota}).", operacao, caminho);

            throw new MelhorEnvioApiException(
                $"Nao foi possivel {operacao} agora: o servico de frete nao respondeu a tempo. Tente novamente em instantes.",
                statusCode: null,
                corpoBruto: null,
                innerException: excecao);
        }
        catch (HttpRequestException excecao)
        {
            _logger.LogError(excecao, "Falha de rede ao {Operacao} no servico de frete ({Rota}).", operacao, caminho);

            throw new MelhorEnvioApiException(
                $"Nao foi possivel {operacao} agora: o servico de frete esta indisponivel. Tente novamente em instantes.",
                statusCode: null,
                corpoBruto: null,
                innerException: excecao);
        }

        using (resposta)
        {
            var bruto = await resposta.Content.ReadAsStringAsync(ct);

            if (!resposta.IsSuccessStatusCode)
            {
                var status = (int)resposta.StatusCode;
                var mensagem = ExtrairMensagem(bruto, status, operacao);

                _logger.LogWarning(
                    "Servico de frete recusou {Operacao} ({Rota}) com HTTP {Status}: {Mensagem}",
                    operacao, caminho, status, mensagem);

                throw new MelhorEnvioApiException(mensagem, status, bruto);
            }

            if (string.IsNullOrWhiteSpace(bruto))
                throw new MelhorEnvioApiException(
                    $"O servico de frete respondeu vazio ao {operacao}.",
                    statusCode: 502,
                    corpoBruto: null);

            try
            {
                // Clone: o JsonDocument e descartavel e o JsonElement dele morre junto. Sem o
                // clone o chamador recebe um elemento apontando para memoria ja liberada.
                using var documento = JsonDocument.Parse(bruto);
                return (documento.RootElement.Clone(), bruto);
            }
            catch (JsonException excecao)
            {
                throw new MelhorEnvioApiException(
                    $"O servico de frete respondeu em formato invalido ao {operacao}.",
                    statusCode: 502,
                    corpoBruto: bruto,
                    innerException: excecao);
            }
        }
    }

    /// <summary>
    /// accountId em TODA rota — inclusive nas de auth. E a chave multi-tenant da tabela de
    /// tokens do microservico; sem ela ele cai na conta "default", que em producao nao existe.
    /// </summary>
    private string ComAccountId(string caminho)
    {
        var separador = caminho.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{caminho}{separador}accountId={Uri.EscapeDataString(_opcoes.ContaId ?? string.Empty)}";
    }

    /// <summary>
    /// Extrai a mensagem util do ProblemDetails do microservico.
    ///
    /// O erro de validacao REAL do Melhor Envio viaja dentro de "detail", em texto
    /// ("... (HTTP 422). Corpo: {json}"). Nao existe campo estruturado com o payload de erro do
    /// parceiro — quem quiser o JSON original parseia o detail (ou le CorpoBruto na excecao).
    /// </summary>
    private static string ExtrairMensagem(string corpo, int status, string operacao)
    {
        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using var documento = JsonDocument.Parse(corpo);
                var raiz = documento.RootElement;

                var mensagem = MelhorEnvioJson.Texto(raiz, "detail")
                               ?? MelhorEnvioJson.Texto(raiz, "message")
                               ?? MelhorEnvioJson.Texto(raiz, "error")
                               ?? MelhorEnvioJson.Texto(raiz, "title");

                if (!string.IsNullOrWhiteSpace(mensagem))
                    return mensagem;
            }
            catch (JsonException)
            {
                // 401 do microservico sai com CORPO VAZIO e sem ProblemDetails; outros
                // intermediarios (proxy, gateway) devolvem HTML. Cair na mensagem padrao.
            }
        }

        return status == 401
            ? $"O servico de frete recusou nossa credencial ao {operacao}. Verifique MelhorEnvio:ApiKey."
            : $"O servico de frete recusou a operacao ({operacao}) com HTTP {status}.";
    }

    private static IReadOnlyList<string> NormalizarIds(IReadOnlyList<string>? ids, string acao)
    {
        var limpos = (ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (limpos.Length == 0)
            throw new BusinessValidationException($"Informe ao menos uma etiqueta para {acao}.");

        return limpos;
    }

    private static PartePayload ParaPayload(ParteEnvioInfo parte) => new()
    {
        Name = parte.Nome,
        Email = parte.Email,
        Phone = parte.Telefone,
        Document = parte.Documento,
        CompanyDocument = parte.DocumentoEmpresa,
        StateRegister = parte.InscricaoEstadual,
        EconomicActivityCode = parte.CodigoAtividadeEconomica,
        Address = parte.Logradouro,
        Number = parte.Numero,
        Complement = parte.Complemento,
        District = parte.Bairro,
        City = parte.Cidade,
        PostalCode = parte.Cep,
        StateAbbr = parte.Uf,
        CountryId = string.IsNullOrWhiteSpace(parte.PaisId) ? "BR" : parte.PaisId,
        Note = parte.Observacao
    };

    /// <summary>
    /// Status textual do Melhor Envio para o nosso enum.
    ///
    /// Null quando nao ha equivalente conhecido — e o consumidor grava o evento e NAO mexe no
    /// status local. Chutar um equivalente aqui e o que faria um pedido entregue voltar para
    /// "postado" na tela do cliente quando o parceiro inventasse um estado novo.
    /// </summary>
    private static StatusEnvio? TraduzirStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "released" or "paid" => StatusEnvio.Comprado,
            "generated" => StatusEnvio.EtiquetaGerada,
            "posted" => StatusEnvio.Postado,
            "delivered" => StatusEnvio.Entregue,
            "canceled" or "cancelled" => StatusEnvio.Cancelado,
            _ => null
        };

    /// <summary>
    /// Historico de rastreio para a timeline do cliente (envios_eventos).
    /// O nome do array muda entre respostas do parceiro, entao os tres candidatos sao tentados.
    /// </summary>
    private static IReadOnlyList<RastreioEventoInfo> ExtrairEventos(JsonElement item)
    {
        JsonElement lista = default;

        var encontrou = MelhorEnvioJson.TentarObter(item, "tracking_events", out lista)
                        || MelhorEnvioJson.TentarObter(item, "events", out lista)
                        || MelhorEnvioJson.TentarObter(item, "history", out lista);

        if (!encontrou || lista.ValueKind != JsonValueKind.Array)
            return [];

        var eventos = new List<RastreioEventoInfo>();

        foreach (var evento in lista.EnumerateArray())
        {
            if (evento.ValueKind != JsonValueKind.Object)
                continue;

            eventos.Add(new RastreioEventoInfo
            {
                DataUtc = MelhorEnvioJson.DataUtc(evento, "date")
                          ?? MelhorEnvioJson.DataUtc(evento, "created_at")
                          ?? MelhorEnvioJson.DataUtc(evento, "occurred_at"),
                Descricao = MelhorEnvioJson.Texto(evento, "description")
                            ?? MelhorEnvioJson.Texto(evento, "status")
                            ?? MelhorEnvioJson.Texto(evento, "message"),
                Local = MelhorEnvioJson.Texto(evento, "location")
                        ?? MelhorEnvioJson.Texto(evento, "city")
            });
        }

        return eventos;
    }

    /// <summary>Espelho de TokenStatusResponse do microservico (camelCase, unico tipado).</summary>
    private sealed record StatusContaPayload
    {
        public bool Connected { get; init; }
        public string? AccountId { get; init; }
        public string? TokenType { get; init; }
        public string? Scope { get; init; }
        public DateTimeOffset? ExpiresAtUtc { get; init; }
        public long? ExpiresInSeconds { get; init; }
        public bool NeedsRefresh { get; init; }
    }
}
