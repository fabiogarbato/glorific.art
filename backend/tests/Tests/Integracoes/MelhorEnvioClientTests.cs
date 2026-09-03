using System.Net;
using System.Text.Json;
using Glorific.Application.Common;
using Glorific.Application.Exceptions;
using Glorific.Application.Models.MelhorEnvio;
using Glorific.Application.Ports.Options;
using Glorific.Infrastructure.Integrations.MelhorEnvio;
using Glorific.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glorific.Tests.Integracoes;

/// <summary>
/// Testes do adaptador do microservico integracaoMelhorEnvio, com o transporte substituido por
/// <see cref="CapturingHandler"/>. NENHUM byte sai da maquina: o host aponta para ".invalid".
///
/// Asserção nos dois lados do fio, porque os dois ja quebraram em producao no repo de referencia:
/// o que foi ENVIADO (rota, metodo, X-Api-Key, unidades do corpo) e o que o adaptador FEZ com a
/// resposta (item indisponivel, 401, timeout, corpo cru preservado para raw_ultima_resposta).
/// </summary>
public sealed class MelhorEnvioClientTests
{
    private const string BaseUrl = "http://melhorenvio-teste.invalid:8080";
    private const string ApiKey = "chave-de-teste";
    private const string ContaId = "glorific";

    private const string RotaCotacao = "/api/shipment/calculate";
    private const string RotaCarrinho = "/api/cart";

    // ------------------------------------------------------------------
    // 1. O que sai pelo fio na cotacao
    // ------------------------------------------------------------------

    [Fact]
    public async Task CotarFreteAsync_ComProdutos_EnviaPostNaRotaDeCotacaoComApiKeyEContaId()
    {
        var handler = CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos);
        var cliente = Criar(handler);

        await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        var enviado = handler.Unica;

        Assert.Equal(HttpMethod.Post, enviado.Metodo);
        Assert.Equal(RotaCotacao, enviado.Caminho);
        Assert.Equal("application/json", enviado.ContentType);

        // X-Api-Key vem de DefaultRequestHeaders montado no construtor. Sem ele TODA rota do
        // microservico responde 401 com corpo vazio.
        Assert.Equal(ApiKey, enviado.Cabecalho("X-Api-Key"));

        // accountId em toda rota: e a chave multi-tenant da tabela de tokens do microservico.
        // Sem ela ele cai na conta "default", que em producao nao existe.
        Assert.Equal(ContaId, enviado.ValorDaQuery("accountId"));
    }

    [Fact]
    public async Task CotarFreteAsync_ComProdutos_EnviaPesoEDimensoesDeCadaProdutoNoFormatoDoServico()
    {
        var handler = CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos);
        var cliente = Criar(handler);

        await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        var corpo = handler.Unica.CorpoJson;

        // Contrato de ENTRADA do microservico e camelCase: "postal_code" nao liga, porque
        // case-insensitive nao remove underscore.
        Assert.Equal("80010000", corpo.GetProperty("from").GetProperty("postalCode").GetString());
        Assert.Equal("01310100", corpo.GetProperty("to").GetProperty("postalCode").GetString());

        var produtos = corpo.GetProperty("products");
        Assert.Equal(2, produtos.GetArrayLength());

        var vestido = produtos[0];
        Assert.Equal("variacao-1", vestido.GetProperty("id").GetString());
        Assert.Equal(20m, vestido.GetProperty("width").GetDecimal());
        Assert.Equal(5m, vestido.GetProperty("height").GetDecimal());
        Assert.Equal(30m, vestido.GetProperty("length").GetDecimal());

        // Peso em KG decimal. 450 g viram 0,450 kg — o erro do repo de referencia era mandar
        // 450 e cotar 450 kg para um vestido.
        Assert.Equal(0.450m, vestido.GetProperty("weight").GetDecimal());

        // Valor declarado em REAIS decimais, nao nos centavos que trafegam dentro do sistema.
        Assert.Equal(189.90m, vestido.GetProperty("insuranceValue").GetDecimal());
        Assert.Equal(2, vestido.GetProperty("quantity").GetInt32());

        var bolsa = produtos[1];
        Assert.Equal(1.200m, bolsa.GetProperty("weight").GetDecimal());
        Assert.Equal(99.90m, bolsa.GetProperty("insuranceValue").GetDecimal());
        Assert.Equal(1, bolsa.GetProperty("quantity").GetInt32());

        // products OU volumes, nunca os dois: o microservico devolve 400 quando ambos viajam.
        Assert.False(corpo.TryGetProperty("volumes", out _));

        Assert.False(corpo.GetProperty("options").GetProperty("receipt").GetBoolean());
        Assert.False(corpo.GetProperty("options").GetProperty("ownHand").GetBoolean());

        // CSV de ids de servico, nao array.
        Assert.Equal("1,2", corpo.GetProperty("services").GetString());
    }

    [Fact]
    public async Task CotarFreteAsync_ComQuantidadeZerada_EnviaQuantidadeUm()
    {
        var handler = CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos);
        var cliente = Criar(handler);

        await cliente.CotarFreteAsync(new CotacaoFreteRequisicao
        {
            CepOrigem = "80010000",
            CepDestino = "01310100",
            Produtos =
            [
                new CotacaoProdutoInfo
                {
                    Id = "variacao-1",
                    LarguraCm = 20m,
                    AlturaCm = 5m,
                    ComprimentoCm = 30m,
                    PesoKg = FreteConversoes.GramasParaKg(450),
                    ValorSeguradoCentavos = 18990,
                    Quantidade = 0
                }
            ]
        });

        var produto = handler.Unica.CorpoJson.GetProperty("products")[0];

        // Quantidade zero faz o ME recusar a cotacao inteira com 422.
        Assert.Equal(1, produto.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task CotarFreteAsync_SemProdutosENemVolumes_LancaValidacaoSemIrAoServico()
    {
        var handler = CapturingHandler.ComJsonOk("[]");
        var cliente = Criar(handler);

        var requisicao = new CotacaoFreteRequisicao { CepOrigem = "80010000", CepDestino = "01310100" };

        await Assert.ThrowsAsync<BusinessValidationException>(() => cliente.CotarFreteAsync(requisicao));

        Assert.Equal(0, handler.Chamadas);
    }

    [Fact]
    public async Task CotarFreteAsync_ComProdutosEVolumesJuntos_LancaValidacaoSemIrAoServico()
    {
        var handler = CapturingHandler.ComJsonOk("[]");
        var cliente = Criar(handler);

        var requisicao = RequisicaoDeCotacao() with
        {
            Volumes =
            [
                new CotacaoVolumeInfo
                {
                    LarguraCm = 30m, AlturaCm = 10m, ComprimentoCm = 40m,
                    PesoKg = 1.5m, ValorSeguradoCentavos = 28980
                }
            ]
        };

        await Assert.ThrowsAsync<BusinessValidationException>(() => cliente.CotarFreteAsync(requisicao));

        Assert.Equal(0, handler.Chamadas);
    }

    [Fact]
    public async Task CotarFreteAsync_SemApiKeyConfigurada_NaoEnviaOHeaderEmVezDeEnviarVazio()
    {
        var handler = CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos);

        var cliente = new MelhorEnvioClient(
            new HttpClient(handler),
            Options.Create(new MelhorEnvioOptions { BaseUrl = BaseUrl, ApiKey = "  ", ContaId = ContaId }),
            NullLogger<MelhorEnvioClient>.Instance);

        await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        Assert.False(handler.Unica.TemCabecalho("X-Api-Key"));
    }

    // ------------------------------------------------------------------
    // 2. Parsing da resposta
    // ------------------------------------------------------------------

    [Fact]
    public async Task CotarFreteAsync_ComRespostaValida_UsaCustomPriceEConverteParaCentavos()
    {
        var cliente = Criar(CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos));

        var resultados = await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        var pac = resultados.Single(r => r.IdServico == 1);

        Assert.Equal("PAC", pac.NomeServico);
        Assert.Equal("Correios", pac.NomeTransportadora);

        // custom_price ("22.41") e o preco COM desconto da conta — e o que o ME debita da
        // carteira, e portanto o unico numero honesto para cobrar do cliente.
        Assert.Equal(2241, pac.PrecoCentavos);
        Assert.Equal(2490, pac.PrecoTabelaCentavos);
        Assert.Equal(249, pac.DescontoCentavos);
        Assert.Equal(8, pac.PrazoDias);
        Assert.True(pac.Disponivel);
    }

    [Fact]
    public async Task CotarFreteAsync_ComServicoIndisponivel_MarcaItemComoIndisponivelSemLancar()
    {
        var cliente = Criar(CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos));

        var resultados = await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        // O ME devolve o servico indisponivel COM "error" preenchido em vez de sumir da lista.
        // Uma excecao aqui derrubaria a cotacao INTEIRA por causa de uma transportadora.
        Assert.Equal(2, resultados.Count);

        var sedex = resultados.Single(r => r.IdServico == 2);

        Assert.False(sedex.Disponivel);
        Assert.Equal("Servico indisponivel para o trecho informado.", sedex.Erro);

        // O servico bom continua utilizavel na mesma resposta.
        Assert.True(resultados.Single(r => r.IdServico == 1).Disponivel);
    }

    [Fact]
    public async Task CotarFreteAsync_ComCampoErroEmObjeto_NaoQuebraOParsingDaCotacaoInteira()
    {
        // "error" ja chegou string, objeto e lista do parceiro. Desserializar como string
        // derrubaria a cotacao toda por causa de um unico servico.
        const string json = """
        [
          {"id":1,"name":"PAC","price":"24.90","custom_price":"24.90","delivery_time":8},
          {"id":2,"name":"SEDEX","error":{"code":422,"message":"CEP fora de area"}}
        ]
        """;

        var cliente = Criar(CapturingHandler.ComJsonOk(json));

        var resultados = await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        Assert.Equal(2, resultados.Count);

        var sedex = resultados.Single(r => r.IdServico == 2);

        Assert.False(sedex.Disponivel);
        Assert.Contains("CEP fora de area", sedex.Erro);
    }

    [Fact]
    public async Task CotarFreteAsync_ComRespostaDeObjetoUnico_DevolveUmResultado()
    {
        // Com "services" de um id so — o caso da recotacao do checkout — o parceiro devolve
        // OBJETO, e nao array.
        const string json = """
        {"id":2,"name":"SEDEX","price":41.30,"custom_price":37.17,"delivery_time":3}
        """;

        var cliente = Criar(CapturingHandler.ComJsonOk(json));

        var resultados = await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        var unico = Assert.Single(resultados);

        Assert.Equal(2, unico.IdServico);
        Assert.Equal(3717, unico.PrecoCentavos);
    }

    [Fact]
    public async Task CotarFreteAsync_ComItemSemIdDeServico_DescartaOItem()
    {
        const string json = """
        [
          {"name":"Sem id","price":"10.00"},
          {"id":0,"name":"Id zerado","price":"10.00"},
          {"id":1,"name":"PAC","price":"24.90","custom_price":"24.90"}
        ]
        """;

        var cliente = Criar(CapturingHandler.ComJsonOk(json));

        var resultados = await cliente.CotarFreteAsync(RequisicaoDeCotacao());

        // Linha sem id nao serve: e o id que vai em "service" no POST /api/cart.
        var unico = Assert.Single(resultados);
        Assert.Equal(1, unico.IdServico);
    }

    // ------------------------------------------------------------------
    // 3. Erro do servico vira mensagem de dominio, nunca vazamento tecnico
    // ------------------------------------------------------------------

    [Fact]
    public async Task CotarFreteAsync_ComRespostaNaoAutorizada_LancaMensagemDeDominioSemVazarStackTrace()
    {
        // 401 do microservico sai com CORPO VAZIO e sem ProblemDetails.
        var cliente = Criar(CapturingHandler.ComCorpoVazio(HttpStatusCode.Unauthorized));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.Equal(401, excecao.StatusCode);
        Assert.True(excecao.EhErroCliente);
        Assert.False(excecao.EhFalhaComunicacao);

        Assert.Equal(
            "O servico de frete recusou nossa credencial ao cotar o frete. Verifique MelhorEnvio:ApiKey.",
            excecao.Message);

        // A mensagem e para humano de operacao: nada de tipo de excecao, namespace ou stack.
        Assert.DoesNotContain("Exception", excecao.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Glorific.", excecao.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", excecao.Message, StringComparison.Ordinal);
        Assert.Null(excecao.InnerException);
    }

    [Fact]
    public async Task CotarFreteAsync_ComProblemDetailsDoServico_UsaODetailComoMensagem()
    {
        const string problema = """
        {"type":"about:blank","title":"Erro na API do Melhor Envio","status":422,
         "detail":"Melhor Envio retornou erro em calculate (HTTP 422). Corpo: {\"errors\":{\"to.postal_code\":[\"CEP invalido\"]}}"}
        """;

        var cliente = Criar(CapturingHandler.ComJson(HttpStatusCode.UnprocessableEntity, problema));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.Equal(422, excecao.StatusCode);
        Assert.Contains("CEP invalido", excecao.Message);

        // O corpo cru vem junto: e a unica forma de reconstruir o erro de validacao do parceiro.
        Assert.Equal(problema, excecao.CorpoBruto);
    }

    [Fact]
    public async Task CotarFreteAsync_ComRespostaHtmlDeProxy_NaoVazaOHtmlNaMensagem()
    {
        var cliente = Criar(CapturingHandler.ComTexto(
            HttpStatusCode.BadGateway,
            "<html><head><title>502 Bad Gateway</title></head><body>nginx</body></html>",
            "text/html"));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.Equal("O servico de frete recusou a operacao (cotar o frete) com HTTP 502.", excecao.Message);
        Assert.DoesNotContain("<html", excecao.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(excecao.EhFalhaComunicacao);
    }

    [Fact]
    public async Task CotarFreteAsync_ComTimeoutDoServico_LancaMensagemDeNaoFoiPossivelCotarAgora()
    {
        var cliente = Criar(CapturingHandler.ComoTimeout());

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.StartsWith("Nao foi possivel cotar o frete agora", excecao.Message, StringComparison.Ordinal);
        Assert.Contains("nao respondeu a tempo", excecao.Message, StringComparison.Ordinal);

        // Sem status: nao houve resposta. E o que separa "o parceiro recusou" de "o parceiro
        // nao respondeu" — a primeira e culpa do dado, a segunda e indisponibilidade.
        Assert.Null(excecao.StatusCode);
        Assert.True(excecao.EhFalhaComunicacao);
        Assert.False(excecao.EhErroCliente);
    }

    [Fact]
    public async Task CotarFreteAsync_ComFalhaDeRede_LancaMensagemDeServicoIndisponivel()
    {
        var cliente = Criar(CapturingHandler.QueLanca(
            () => new HttpRequestException("Connection refused")));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.StartsWith("Nao foi possivel cotar o frete agora", excecao.Message, StringComparison.Ordinal);
        Assert.Contains("esta indisponivel", excecao.Message, StringComparison.Ordinal);
        Assert.Null(excecao.StatusCode);
    }

    [Fact]
    public async Task CotarFreteAsync_ComCancelamentoDoChamador_PropagaCancelamentoENaoVira502()
    {
        // Cancelamento do cliente NAO e incidente do parceiro: nao pode virar MelhorEnvioApiException.
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var cliente = Criar(CapturingHandler.ComJsonOk(JsonCotacaoComDoisServicos));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao(), cancelamento.Token));
    }

    [Fact]
    public async Task CotarFreteAsync_ComRespostaVazia_LancaFalhaDeFormato()
    {
        var cliente = Criar(CapturingHandler.ComTexto(HttpStatusCode.OK, string.Empty, "application/json"));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.Equal(502, excecao.StatusCode);
        Assert.Contains("respondeu vazio", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CotarFreteAsync_ComJsonInvalido_LancaFalhaDeFormatoComOCorpoCru()
    {
        const string lixo = "{ isto nao e json";

        var cliente = Criar(CapturingHandler.ComTexto(HttpStatusCode.OK, lixo, "application/json"));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.CotarFreteAsync(RequisicaoDeCotacao()));

        Assert.Equal(502, excecao.StatusCode);
        Assert.Contains("formato invalido", excecao.Message, StringComparison.Ordinal);
        Assert.Equal(lixo, excecao.CorpoBruto);
    }

    // ------------------------------------------------------------------
    // 4. O JSON cru e preservado para envios.raw_ultima_resposta (jsonb)
    // ------------------------------------------------------------------

    [Fact]
    public async Task InserirNoCarrinhoAsync_ComRespostaDoServico_PreservaOJsonCruByteAByte()
    {
        // Formatacao esquisita e campos desconhecidos de proposito: raw_ultima_resposta existe
        // justamente para o dia em que o parceiro muda o formato sem avisar. Reserializar aqui
        // apagaria a evidencia.
        const string cru = "{\n  \"id\": \"1f8a4c22-0000-4b3a-9a11-9b2c3d4e5f60\",\n"
                           + "  \"protocol\": \"ORD-2026-0001\",\n  \"status\": \"pending\",\n"
                           + "  \"price\": 24.90,\n  \"service_id\": 1,\n"
                           + "  \"campo_que_nao_conhecemos\": {\"beta\": true}\n}";

        var handler = CapturingHandler.ComJson(HttpStatusCode.Created, cru);
        var cliente = Criar(handler);

        var resultado = await cliente.InserirNoCarrinhoAsync(RequisicaoDeCarrinho());

        Assert.Equal(cru, resultado.RawJson);

        Assert.Equal("1f8a4c22-0000-4b3a-9a11-9b2c3d4e5f60", resultado.MeOrderId);
        Assert.Equal("ORD-2026-0001", resultado.Protocolo);
        Assert.Equal("pending", resultado.Status);
        Assert.Equal(2490, resultado.PrecoCentavos);
        Assert.Equal(1, resultado.IdServico);
    }

    [Fact]
    public async Task InserirNoCarrinhoAsync_ComProdutoDeclarado_EnviaQuantidadeEValorComoTexto()
    {
        var handler = CapturingHandler.ComJson(
            HttpStatusCode.Created,
            """{"id":"1f8a4c22-0000-4b3a-9a11-9b2c3d4e5f60"}""");

        var cliente = Criar(handler);

        await cliente.InserirNoCarrinhoAsync(RequisicaoDeCarrinho());

        var enviado = handler.Unica;

        Assert.Equal(HttpMethod.Post, enviado.Metodo);
        Assert.Equal(RotaCarrinho, enviado.Caminho);
        Assert.Equal(ApiKey, enviado.Cabecalho("X-Api-Key"));

        var produto = enviado.CorpoJson.GetProperty("products")[0];

        // quantity e unitaryValue sao STRING no contrato do parceiro — nao e engano de tipagem.
        Assert.Equal(JsonValueKind.String, produto.GetProperty("quantity").ValueKind);
        Assert.Equal(JsonValueKind.String, produto.GetProperty("unitaryValue").ValueKind);

        Assert.Equal("2", produto.GetProperty("quantity").GetString());

        // Ponto como separador decimal: em pt-BR sairia "189,90" e o parser do parceiro recusa.
        Assert.Equal("189.90", produto.GetProperty("unitaryValue").GetString());

        // district nunca vazio: o ME RECUSA o carrinho sem bairro.
        Assert.Equal("Centro", enviado.CorpoJson.GetProperty("to").GetProperty("district").GetString());

        // invoice e nonCommercial somem do payload quando nao se aplicam (WhenWritingNull);
        // enviar objeto vazio faz o ME tratar como declaracao de conteudo invalida.
        var opcoes = enviado.CorpoJson.GetProperty("options");
        Assert.False(opcoes.TryGetProperty("invoice", out _));
        Assert.False(opcoes.TryGetProperty("nonCommercial", out _));
    }

    [Fact]
    public async Task InserirNoCarrinhoAsync_SemIdentificadorNaResposta_LancaComOCorpoCruPreservado()
    {
        const string semId = """{"protocol":"ORD-2026-0001","status":"pending"}""";

        var cliente = Criar(CapturingHandler.ComJson(HttpStatusCode.Created, semId));

        var excecao = await Assert.ThrowsAsync<MelhorEnvioApiException>(
            () => cliente.InserirNoCarrinhoAsync(RequisicaoDeCarrinho()));

        Assert.Equal(502, excecao.StatusCode);

        // Sem o uuid o fluxo inteiro para; o cru fica para investigar o que o parceiro devolveu.
        Assert.Equal(semId, excecao.CorpoBruto);
    }

    [Fact]
    public async Task ConsultarSaldoAsync_ComRespostaDoServico_PreservaOJsonCruEConverteParaCentavos()
    {
        const string cru = """{"balance":"152.37","currency":"BRL"}""";

        var handler = CapturingHandler.ComJsonOk(cru);
        var cliente = Criar(handler);

        var saldo = await cliente.ConsultarSaldoAsync();

        Assert.Equal(HttpMethod.Get, handler.Unica.Metodo);
        Assert.Equal("/api/me/balance", handler.Unica.Caminho);
        Assert.Null(handler.Unica.Corpo);

        Assert.Equal(15237, saldo.SaldoCentavos);
        Assert.Equal("BRL", saldo.Moeda);
        Assert.Equal(cru, saldo.RawJson);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static MelhorEnvioClient Criar(CapturingHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new MelhorEnvioOptions
            {
                BaseUrl = BaseUrl,
                ApiKey = ApiKey,
                ContaId = ContaId
            }),
            NullLogger<MelhorEnvioClient>.Instance);

    private static CotacaoFreteRequisicao RequisicaoDeCotacao() => new()
    {
        CepOrigem = "80010000",
        CepDestino = "01310100",
        Produtos =
        [
            new CotacaoProdutoInfo
            {
                Id = "variacao-1",
                LarguraCm = 20m,
                AlturaCm = 5m,
                ComprimentoCm = 30m,
                PesoKg = FreteConversoes.GramasParaKg(450),
                ValorSeguradoCentavos = 18990,
                Quantidade = 2
            },
            new CotacaoProdutoInfo
            {
                Id = "variacao-2",
                LarguraCm = 15m,
                AlturaCm = 4m,
                ComprimentoCm = 25m,
                PesoKg = FreteConversoes.GramasParaKg(1200),
                ValorSeguradoCentavos = 9990,
                Quantidade = 1
            }
        ],
        Servicos = [1, 2]
    };

    private static CarrinhoEnvioRequisicao RequisicaoDeCarrinho() => new()
    {
        IdServico = 1,
        Remetente = new ParteEnvioInfo
        {
            Nome = "Glorific",
            Logradouro = "Rua das Flores",
            Numero = "100",
            Bairro = "Batel",
            Cidade = "Curitiba",
            Cep = "80010000",
            Uf = "PR",
            DocumentoEmpresa = "12345678000199"
        },
        Destinatario = new ParteEnvioInfo
        {
            Nome = "Maria Souza",
            Documento = "12345678909",
            Logradouro = "Avenida Paulista",
            Numero = "1000",
            Bairro = "Centro",
            Cidade = "Sao Paulo",
            Cep = "01310100",
            Uf = "SP"
        },
        Produtos =
        [
            new ProdutoDeclaradoInfo
            {
                Nome = "Vestido Midi Linho - M / Terracota",
                Quantidade = 2,
                ValorUnitarioCentavos = 18990,
                PesoKg = 0.450m
            }
        ],
        Volumes =
        [
            new VolumeEnvioInfo { AlturaCm = 10m, LarguraCm = 30m, ComprimentoCm = 40m, PesoKg = 0.9m }
        ],
        Opcoes = new OpcoesEnvioInfo
        {
            Plataforma = "glorific.art",
            ValorSeguradoCentavos = 37980,
            Tags = [new EtiquetaTagInfo { Tag = "GA-2026-000137" }]
        }
    };

    /// <summary>
    /// Resposta REPASSADA do Melhor Envio (snake_case, price como string) com um servico
    /// disponivel e um indisponivel na MESMA lista.
    /// </summary>
    private const string JsonCotacaoComDoisServicos = """
    [
      {
        "id": 1,
        "name": "PAC",
        "price": "24.90",
        "custom_price": "22.41",
        "discount": "2.49",
        "delivery_time": 8,
        "custom_delivery_time": 8,
        "company": { "id": 1, "name": "Correios", "picture": "https://melhorenvio.test/correios.png" }
      },
      {
        "id": 2,
        "name": "SEDEX",
        "price": "41.30",
        "delivery_time": 3,
        "company": { "id": 1, "name": "Correios", "picture": "https://melhorenvio.test/correios.png" },
        "error": "Servico indisponivel para o trecho informado."
      }
    ]
    """;
}
