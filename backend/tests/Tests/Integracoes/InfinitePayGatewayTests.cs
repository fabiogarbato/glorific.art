using System.Net;
using System.Text.Json;
using Glorific.Application.Models.Pagamento;
using Glorific.Application.Ports.Options;
using Glorific.Domain.Interfaces;
using Glorific.Infrastructure.Integrations.InfinitePay;
using Glorific.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glorific.Tests.Integracoes;

/// <summary>
/// Testes do adaptador da InfinitePay com o transporte substituido por
/// <see cref="CapturingHandler"/>. Nenhuma chamada de rede real.
///
/// Esta e a integracao mais perigosa do sistema: a InfinitePay NAO tem chave secreta nem
/// assinatura HMAC, e a conta e identificada por um handle publico. Consequencia: nada que chegue
/// de fora prova pagamento, e a conferencia (payment_check) e a UNICA fonte da verdade.
///
/// Os quatro testes centrais reproduzem as falhas exatas do repo de referencia (geb-sul):
/// order_nsu enumeravel, conferencia que aprova sem conferir status, conferencia que aprova sem
/// conferir VALOR, e status novo do provedor tratado como sucesso.
/// </summary>
public sealed class InfinitePayGatewayTests
{
    private const string BaseUrl = "https://infinitepay-teste.invalid";
    private const string CaminhoCheckout = "/invoices/public/checkout/links";
    private const string CaminhoConferencia = "/invoices/public/checkout/payment_check";

    private const string UrlRetorno = "https://api.glorific.art/api/v1/webhooks/pagamento/retorno";
    private const string UrlWebhook = "https://api.glorific.art/api/v1/webhooks/pagamento";

    private static readonly DateTime Agora = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private const string RespostaCheckoutOk = """
    {"url":"https://checkout.infinitepay.io/glorific/abc123","id":"inv_9f2"}
    """;

    // ------------------------------------------------------------------
    // 1. Criacao do checkout: o que sai pelo fio
    // ------------------------------------------------------------------

    [Fact]
    public async Task CriarCheckoutAsync_ComPedidoValido_EnviaHandleItensOrderNsuUrlsECliente()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        var nsu = GerarOrderNsu();
        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(nsu));

        Assert.True(resultado.Sucesso);

        var enviado = handler.Unica;

        Assert.Equal(HttpMethod.Post, enviado.Metodo);
        Assert.Equal(CaminhoCheckout, enviado.Caminho);
        Assert.Equal("application/json", enviado.ContentType);

        var corpo = enviado.CorpoJson;

        // O handle vai SEM arroba, mesmo configurado com ela.
        Assert.Equal("glorific", corpo.GetProperty("handle").GetString());

        Assert.Equal(nsu, corpo.GetProperty("order_nsu").GetString());
        Assert.Equal(UrlRetorno, corpo.GetProperty("redirect_url").GetString());
        Assert.Equal(UrlWebhook, corpo.GetProperty("webhook_url").GetString());

        var itens = corpo.GetProperty("items");
        Assert.Equal(2, itens.GetArrayLength());

        Assert.Equal(2, itens[0].GetProperty("quantity").GetInt32());
        Assert.Equal("Vestido Midi Linho - M / Terracota", itens[0].GetProperty("description").GetString());

        // O frete entra como LINHA PROPRIA da cobranca, com valor flat.
        Assert.Equal(1, itens[1].GetProperty("quantity").GetInt32());
        Assert.Equal("Frete PAC", itens[1].GetProperty("description").GetString());

        var cliente = corpo.GetProperty("customer");
        Assert.Equal("Maria Souza", cliente.GetProperty("name").GetString());
        Assert.Equal("maria@exemplo.test", cliente.GetProperty("email").GetString());
        Assert.Equal("41999990000", cliente.GetProperty("phone_number").GetString());

        // O CPF NAO e enviado: a InfinitePay nao pede documento no link de checkout, e mandar
        // dado pessoal que o parceiro nao precisa e superficie de vazamento de graca.
        Assert.False(cliente.TryGetProperty("document", out _));
        Assert.False(cliente.TryGetProperty("cpf", out _));
        Assert.DoesNotContain("12345678909", enviado.Corpo);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComValoresEmCentavos_EnviaPriceInteiroSemMultiplicarPorCem()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));

        var itens = handler.Unica.CorpoJson.GetProperty("items");

        // Dinheiro ja chega em centavos desde o dominio. Nenhuma multiplicacao acontece na
        // fronteira — e justamente a conversao no ultimo instante que produz o erro de 1 centavo.
        Assert.Equal(JsonValueKind.Number, itens[0].GetProperty("price").ValueKind);
        Assert.Equal("18990", itens[0].GetProperty("price").GetRawText());
        Assert.Equal("2241", itens[1].GetProperty("price").GetRawText());

        // Sem casa decimal em nenhum preco: reais decimais aqui seriam cobranca 100x menor.
        Assert.DoesNotContain(".", itens[0].GetProperty("price").GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComItemDeUmReal_EnviaCemCentavos()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        await gateway.CriarCheckoutAsync(new CheckoutRequisicaoInfo
        {
            OrderNsu = GerarOrderNsu(),
            UrlRetorno = UrlRetorno,
            UrlWebhook = UrlWebhook,
            TotalCentavos = 100,
            Itens = [new CheckoutItemInfo { Descricao = "Brinde", Quantidade = 1, PrecoUnitarioCentavos = 100 }]
        });

        Assert.Equal("100", handler.Unica.CorpoJson.GetProperty("items")[0].GetProperty("price").GetRawText());
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComRespostaDoProvedor_DevolveUrlIdExpiracaoEJsonCru()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk(RespostaCheckoutOk));

        var nsu = GerarOrderNsu();
        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(nsu));

        Assert.True(resultado.Sucesso);
        Assert.Equal("https://checkout.infinitepay.io/glorific/abc123", resultado.UrlCheckout);
        Assert.Equal("inv_9f2", resultado.ProviderChargeId);

        // O provedor nao ecoou order_nsu: o adaptador devolve o NOSSO, que e a chave gravada em
        // pagamentos.provider_order_id e a unica correlacao possivel na conferencia.
        Assert.Equal(nsu, resultado.OrderNsu);

        Assert.Equal(RespostaCheckoutOk, resultado.RawJson);

        // A InfinitePay nao devolve validade do link: o prazo e nosso e sai do relogio injetado.
        // E ele que o worker de expiracao usa para devolver a reserva de estoque.
        Assert.Equal(Agora.AddMinutes(1440), resultado.ExpiraEmUtc);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComRespostaSemUrl_DevolveFalhaEmVezDeLancar()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"id":"inv_9f2","status":"created"}"""));

        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));

        // Falha e retorno, nao excecao: quem decide abortar e o CheckoutService, que esta dentro
        // da transacao e precisa dar rollback limpo em pedido e reserva de estoque.
        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.UrlCheckout);
        Assert.Contains("nao devolveu a URL", resultado.Erro);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComErroHttpDoProvedor_DevolveFalhaComOCorpoCru()
    {
        const string erro = """{"message":"handle nao encontrado"}""";

        var gateway = Criar(CapturingHandler.ComJson(HttpStatusCode.BadRequest, erro));

        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));

        Assert.False(resultado.Sucesso);
        Assert.Contains("HTTP 400", resultado.Erro);
        Assert.Equal(erro, resultado.RawJson);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComFalhaDeComunicacao_DevolveFalhaEmVezDeLancar()
    {
        var gateway = Criar(CapturingHandler.QueLanca(() => new HttpRequestException("Connection refused")));

        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nao foi possivel falar com o provedor de pagamento.", resultado.Erro);
    }

    [Fact]
    public async Task CriarCheckoutAsync_ComSomaDasLinhasDivergindoDoTotal_NaoChamaOProvedor()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        var requisicao = RequisicaoDeCheckout(GerarOrderNsu()) with { TotalCentavos = 40000 };

        var resultado = await gateway.CriarCheckoutAsync(requisicao);

        // Se a soma das linhas divergir do total, a conferencia de VALOR nunca fecharia e todo
        // pedido cairia em revisao manual. Falhar antes de sair e barato.
        Assert.False(resultado.Sucesso);
        Assert.Contains("diverge do total", resultado.Erro);
        Assert.Equal(0, handler.Chamadas);
    }

    // ------------------------------------------------------------------
    // 2. order_nsu nao enumeravel (falha #3 do repo de referencia)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1")]
    [InlineData("137")]
    [InlineData("138")]
    [InlineData("20260903000137")]
    public async Task CriarCheckoutAsync_ComOrderNsuSequencial_NaoChamaOProvedorEDevolveFalha(string nsu)
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(nsu));

        // No repo de referencia o order_nsu era "loja-{id}" sequencial: qualquer um enumerava
        // pedidos alheios e forjava retorno de pagamento. Aqui o adaptador barra antes do fio.
        Assert.False(resultado.Sucesso);
        Assert.Contains("nao enumeravel", resultado.Erro);
        Assert.Equal(0, handler.Chamadas);
    }

    [Fact]
    public async Task CriarCheckoutAsync_SemOrderNsu_NaoChamaOProvedorEDevolveFalha()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        var resultado = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout("   "));

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, handler.Chamadas);
    }

    [Fact]
    public async Task CriarCheckoutAsync_DoisCheckoutsSeguidos_EnviamOrderNsuDiferenteENaoAdjacente()
    {
        var handler = CapturingHandler.ComJsonOk(RespostaCheckoutOk);
        var gateway = Criar(handler);

        var primeiro = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));
        var segundo = await gateway.CriarCheckoutAsync(RequisicaoDeCheckout(GerarOrderNsu()));

        Assert.True(primeiro.Sucesso);
        Assert.True(segundo.Sucesso);
        Assert.Equal(2, handler.Chamadas);

        var nsu1 = handler.Requisicoes[0].CorpoJson.GetProperty("order_nsu").GetString()!;
        var nsu2 = handler.Requisicoes[1].CorpoJson.GetProperty("order_nsu").GetString()!;

        Assert.NotEqual(nsu1, nsu2);

        // Nenhum dos dois e puramente numerico: nao ha contador para incrementar.
        Assert.False(nsu1.All(char.IsDigit));
        Assert.False(nsu2.All(char.IsDigit));

        // "Nao adjacente": um sequencial difere do anterior em UM caractere. Dois identificadores
        // gerados pela regra do CheckoutService (prefixo + GUID) divergem em dezenas de posicoes.
        Assert.True(
            PosicoesDiferentes(nsu1, nsu2) >= 8,
            $"order_nsu adjacente demais para ser imprevisivel: '{nsu1}' e '{nsu2}'.");
    }

    // ------------------------------------------------------------------
    // 3. Conferencia: o que sai pelo fio
    // ------------------------------------------------------------------

    [Fact]
    public async Task ConsultarPagamentoAsync_ComOrderNsu_EnviaGetComHandleECorrelacaoNaQuery()
    {
        var handler = CapturingHandler.ComJsonOk("""{"status":"paid","paid_amount":21231}""");
        var gateway = Criar(handler);

        await gateway.ConsultarPagamentoAsync("glo-abc123", "trx-77", "credit_card");

        var enviado = handler.Unica;

        Assert.Equal(HttpMethod.Get, enviado.Metodo);
        Assert.Equal(CaminhoConferencia, enviado.Caminho);
        Assert.Null(enviado.Corpo);

        Assert.Equal("glorific", enviado.ValorDaQuery("handle"));
        Assert.Equal("glo-abc123", enviado.ValorDaQuery("order_nsu"));
        Assert.Equal("trx-77", enviado.ValorDaQuery("transaction_nsu"));
        Assert.Equal("credit_card", enviado.ValorDaQuery("slug"));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_SemTransactionNsuNemSlug_OmiteOsParametrosOpcionais()
    {
        var handler = CapturingHandler.ComJsonOk("""{"status":"pending"}""");
        var gateway = Criar(handler);

        await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.False(handler.Unica.TemNaQuery("transaction_nsu"));
        Assert.False(handler.Unica.TemNaQuery("slug"));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_SemOrderNsu_NaoChamaOProvedor()
    {
        var handler = CapturingHandler.ComJsonOk("""{"status":"paid"}""");
        var gateway = Criar(handler);

        var resultado = await gateway.ConsultarPagamentoAsync("   ");

        Assert.False(resultado.Encontrado);
        Assert.False(resultado.Aprovado);
        Assert.Equal(InfinitePayGateway.StatusNaoEncontrado, resultado.StatusOriginal);
        Assert.Equal(0, handler.Chamadas);
    }

    // ------------------------------------------------------------------
    // 4. Conferencia que devolve NAO-APROVADO nao aprova (falha #1)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("pending", StatusPagamentoGateway.Pendente)]
    [InlineData("waiting_payment", StatusPagamentoGateway.Pendente)]
    [InlineData("processing", StatusPagamentoGateway.Pendente)]
    [InlineData("authorized", StatusPagamentoGateway.Pendente)]
    [InlineData("refused", StatusPagamentoGateway.Recusado)]
    [InlineData("declined", StatusPagamentoGateway.Recusado)]
    [InlineData("failed", StatusPagamentoGateway.Recusado)]
    [InlineData("expired", StatusPagamentoGateway.Expirado)]
    [InlineData("canceled", StatusPagamentoGateway.Cancelado)]
    [InlineData("refunded", StatusPagamentoGateway.Estornado)]
    [InlineData("chargeback", StatusPagamentoGateway.Estornado)]
    public async Task ConsultarPagamentoAsync_ComStatusNaoAprovado_NaoAprovaMesmoComValorCorreto(
        string statusDoProvedor,
        StatusPagamentoGateway esperado)
    {
        // Valor correto de proposito: o unico motivo para nao aprovar tem que ser o STATUS.
        var json = $$"""{"status":"{{statusDoProvedor}}","amount":21231,"order_nsu":"glo-abc123"}""";

        var gateway = Criar(CapturingHandler.ComJsonOk(json));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.True(resultado.Encontrado);
        Assert.Equal(esperado, resultado.Status);
        Assert.Equal(statusDoProvedor, resultado.StatusOriginal);

        Assert.False(resultado.Aprovado);
        Assert.False(resultado.Aprovado && resultado.ValorConfere(21231));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComPaidFalso_NaoAprova()
    {
        // Sem status textual, "paid": false ainda e uma negativa clara do provedor.
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"paid":false,"amount":21231}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(StatusPagamentoGateway.Pendente, resultado.Status);
        Assert.False(resultado.Aprovado);
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComTransacaoDesconhecida_NaoAprovaEMarcaNaoEncontrado()
    {
        var gateway = Criar(CapturingHandler.ComJson(
            HttpStatusCode.NotFound,
            """{"message":"invoice not found"}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-forjado");

        // O gateway respondeu e nao conhece a transacao: o aviso era provavelmente forjado.
        Assert.False(resultado.Encontrado);
        Assert.False(resultado.Aprovado);
        Assert.Equal(InfinitePayGateway.StatusNaoEncontrado, resultado.StatusOriginal);
        Assert.Equal(StatusPagamentoGateway.Desconhecido, resultado.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ConsultarPagamentoAsync_ComErroDeTransporte_NaoAprovaEMarcaComoInconclusivo(
        HttpStatusCode status)
    {
        var gateway = Criar(CapturingHandler.ComJson(status, """{"error":"indisponivel"}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.False(resultado.Aprovado);

        // INCONCLUSIVO, nao negativa: tratar como "nao pagou" aqui seria o caminho para cancelar
        // pedido de cliente que pagou. O evento fica para nova tentativa.
        Assert.Equal(InfinitePayGateway.StatusFalhaTransporte, resultado.StatusOriginal);
        Assert.NotEqual(InfinitePayGateway.StatusNaoEncontrado, resultado.StatusOriginal);
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComFalhaDeRede_NaoAprovaEMarcaComoInconclusivo()
    {
        var gateway = Criar(CapturingHandler.QueLanca(() => new HttpRequestException("DNS failure")));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.False(resultado.Encontrado);
        Assert.False(resultado.Aprovado);
        Assert.Equal(InfinitePayGateway.StatusFalhaTransporte, resultado.StatusOriginal);
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComRespostaSemCampoReconhecivel_NaoAprova()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"mensagem":"ok"}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.False(resultado.Encontrado);
        Assert.False(resultado.Aprovado);
        Assert.Equal(InfinitePayGateway.StatusNaoEncontrado, resultado.StatusOriginal);
    }

    // ------------------------------------------------------------------
    // 5. Aprovado com valor DIFERENTE do pedido nao aprova (falha #2)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusAprovadoEValorMenorQueOPedido_NaoConfereOValor()
    {
        const int totalDoPedido = 21231;

        // Cliente paga R$ 1,00 num pedido de R$ 212,31. No repo de referencia isto marcava o
        // pedido como pago: o payment_check era chamado e o resultado descartado num catch.
        var gateway = Criar(CapturingHandler.ComJsonOk(
            """{"status":"paid","paid_amount":100,"order_nsu":"glo-abc123"}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(StatusPagamentoGateway.Aprovado, resultado.Status);
        Assert.Equal(100, resultado.ValorCentavos);

        // A regra do sistema e composta: Aprovado E ValorConfere. Aqui a segunda metade barra.
        Assert.False(resultado.ValorConfere(totalDoPedido));
        Assert.False(resultado.Aprovado && resultado.ValorConfere(totalDoPedido));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusAprovadoEValorMaiorQueOPedido_NaoConfereOValor()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk(
            """{"status":"approved","amount":21232}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(21232, resultado.ValorCentavos);

        // Um centavo de diferenca ja nao confere: nao ha margem de tolerancia.
        Assert.False(resultado.ValorConfere(21231));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusAprovadoEValorExato_ConfereOValor()
    {
        const string json = """
        {"status":"paid","paid_amount":21231,"capture_method":"pix","installments":1,
         "transaction_nsu":"trx-77","receipt_url":"https://recibo.test/1",
         "paid_at":"2026-09-03T09:30:00-03:00"}
        """;

        var gateway = Criar(CapturingHandler.ComJsonOk(json));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.True(resultado.Aprovado);
        Assert.True(resultado.ValorConfere(21231));
        Assert.Equal("pix", resultado.Metodo);
        Assert.Equal(1, resultado.Parcelas);
        Assert.Equal("trx-77", resultado.TransactionNsu);
        Assert.Equal("https://recibo.test/1", resultado.UrlComprovante);
        Assert.Equal(new DateTime(2026, 9, 3, 12, 30, 0, DateTimeKind.Utc), resultado.PagoEmUtc);
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComValorEmReaisDecimais_ConverteParaCentavosSemErroDeUmCentavo()
    {
        // O provedor alterna entre centavos inteiros (21231) e reais decimais (212.31).
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"status":"paid","amount":212.31}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(21231, resultado.ValorCentavos);
        Assert.True(resultado.ValorConfere(21231));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComValorPequenoEmReais_NaoChutaPelaMagnitude()
    {
        // O criterio e a presenca de casas decimais, nao o tamanho do numero. Chutar por
        // magnitude transformaria R$ 1,50 em R$ 150,00 na conferencia.
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"status":"paid","amount":1.50}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(150, resultado.ValorCentavos);
        Assert.False(resultado.ValorConfere(15000));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusAprovadoESemValor_NaoAprova()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"status":"paid","order_nsu":"glo-abc123"}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        // Sem numero nao ha o que conferir: aprovar seria confiar no status sozinho.
        Assert.Null(resultado.ValorCentavos);
        Assert.False(resultado.Aprovado);
        Assert.False(resultado.ValorConfere(21231));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComRespostaEmbrulhadaEmData_LeStatusEValorDoObjetoInterno()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk(
            """{"success":true,"data":{"status":"paid","amount":21231,"transaction_nsu":"trx-9"}}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(StatusPagamentoGateway.Aprovado, resultado.Status);
        Assert.Equal(21231, resultado.ValorCentavos);
        Assert.True(resultado.ValorConfere(21231));
    }

    // ------------------------------------------------------------------
    // 6. Status desconhecido cai em Desconhecido e nao aprova
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("em_analise_manual")]
    [InlineData("under_review")]
    [InlineData("partially_paid")]
    [InlineData("status_novo_que_o_provedor_inventou")]
    public async Task ConsultarPagamentoAsync_ComStatusDesconhecido_CaiEmDesconhecidoENaoAprova(string status)
    {
        var json = $$"""{"status":"{{status}}","amount":21231}""";

        var gateway = Criar(CapturingHandler.ComJsonOk(json));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        // Lista FECHADA de status: qualquer coisa fora dela vira Desconhecido, e Desconhecido
        // nunca aprova. Nao decidir e melhor que decidir errado a favor de quem paga.
        Assert.Equal(StatusPagamentoGateway.Desconhecido, resultado.Status);
        Assert.False(resultado.Aprovado);
        Assert.False(resultado.Aprovado && resultado.ValorConfere(21231));

        // O cru fica registrado para auditoria — e a unica forma de descobrir o estado novo.
        Assert.Equal(status, resultado.StatusOriginal);
        Assert.Equal(json, resultado.RawJson);
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusDesconhecidoEPaidVerdadeiro_AindaAssimNaoUsaOStatusTextual()
    {
        // Status textual desconhecido + booleano explicito: o booleano e o unico sinal restante.
        // Documenta o comportamento REAL para que uma mudanca nele apareca aqui, e nao em producao.
        var gateway = Criar(CapturingHandler.ComJsonOk(
            """{"status":"em_analise_manual","paid":true,"amount":21231}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(StatusPagamentoGateway.Aprovado, resultado.Status);
        Assert.Equal("em_analise_manual", resultado.StatusOriginal);

        // Mesmo assim a aprovacao final depende de o valor bater com o total do pedido.
        Assert.False(resultado.Aprovado && resultado.ValorConfere(19990));
        Assert.True(resultado.Aprovado && resultado.ValorConfere(21231));
    }

    [Fact]
    public async Task ConsultarPagamentoAsync_ComStatusEmMaiusculasEComEspacos_NormalizaAntesDeMapear()
    {
        var gateway = Criar(CapturingHandler.ComJsonOk("""{"status":"  PAID  ","amount":21231}"""));

        var resultado = await gateway.ConsultarPagamentoAsync("glo-abc123");

        Assert.Equal(StatusPagamentoGateway.Aprovado, resultado.Status);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static InfinitePayGateway Criar(CapturingHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new InfinitePayOptions
            {
                // Com arroba de proposito: o adaptador tem que remove-la.
                Handle = "@glorific",
                BaseUrl = BaseUrl,
                CheckoutPath = CaminhoCheckout,
                PaymentCheckPath = CaminhoConferencia,
                ExpiracaoMinutos = 1440
            }),
            Options.Create(new AppOptions
            {
                PublicBaseUrl = "https://api.glorific.art",
                LojaBaseUrl = "https://glorific.art"
            }),
            new RelogioFixoInfinitePay(Agora),
            NullLogger<InfinitePayGateway>.Instance);

    /// <summary>
    /// Mesma regra do CheckoutService (PrefixoOrderNsu "glo-" + Guid.NewGuid().ToString("N")).
    /// Fica aqui porque a geracao vive dentro da transacao do checkout, mas o que este teste
    /// prova e o contrato da fronteira: o que sai pelo fio nao pode ser enumeravel.
    /// </summary>
    private static string GerarOrderNsu() => "glo-" + Guid.NewGuid().ToString("N");

    private static int PosicoesDiferentes(string a, string b)
    {
        if (a.Length != b.Length)
            return Math.Max(a.Length, b.Length);

        var diferentes = 0;

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                diferentes++;
        }

        return diferentes;
    }

    private static CheckoutRequisicaoInfo RequisicaoDeCheckout(string orderNsu) => new()
    {
        OrderNsu = orderNsu,
        UrlRetorno = UrlRetorno,
        UrlWebhook = UrlWebhook,

        // 2 x 18990 + 2241 de frete.
        TotalCentavos = 40221,
        Itens =
        [
            new CheckoutItemInfo
            {
                Descricao = "Vestido Midi Linho - M / Terracota",
                Quantidade = 2,
                PrecoUnitarioCentavos = 18990
            },
            new CheckoutItemInfo
            {
                Descricao = "Frete PAC",
                Quantidade = 1,
                PrecoUnitarioCentavos = 2241
            }
        ],
        Cliente = new CheckoutClienteInfo
        {
            Nome = "Maria Souza",
            Email = "maria@exemplo.test",
            Telefone = "41999990000",
            Documento = "12345678909"
        }
    };
}

/// <summary>Relogio congelado: o prazo de expiracao da cobranca sai daqui, nao de DateTime.Now.</summary>
internal sealed class RelogioFixoInfinitePay(DateTime agoraUtc) : IClock
{
    public DateTime UtcNow { get; } = agoraUtc;
}
