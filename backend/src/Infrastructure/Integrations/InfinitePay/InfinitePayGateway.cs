using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Glorific.Application.Models.Pagamento;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Integrations.InfinitePay;

/// <summary>
/// Adaptador da InfinitePay — modelo de CHECKOUT WEB HOSPEDADO (link de pagamento).
///
/// Particularidade do provedor que define todo o desenho: NAO existe chave secreta nem assinatura
/// HMAC. A conta e identificada pelo handle, que e publico. Consequencia direta e inescapavel:
/// nada que chegue de fora prova pagamento. Por isso este adaptador expoe exatamente duas
/// operacoes — criar o link e CONFERIR a transacao — e a conferencia e a unica fonte da verdade.
///
/// Tres decisoes que corrigem falhas reais do repo de referencia:
///
/// 1. CriarCheckoutAsync NAO LANCA em erro do parceiro: devolve CheckoutCriadoInfo.Falha. Quem
///    decide abortar e o CheckoutService, que esta dentro da transacao e precisa dar rollback
///    limpo em pedido e reserva de estoque.
///
/// 2. ConsultarPagamentoAsync nunca "chuta" aprovado. Status que nao reconhecemos vira
///    Desconhecido e o servico nao aprova. No repo de referencia a conferencia era chamada e o
///    resultado descartado num catch — e o pedido virava pago de qualquer jeito.
///
/// 3. As URLs sao montadas a partir das options, nao do BaseAddress do HttpClient. Se alguem
///    resolver este adaptador com um client sem BaseAddress configurado, a chamada falha por
///    configuracao e nao vira um 404 num host errado.
/// </summary>
public sealed class InfinitePayGateway : IPaymentGateway
{
    /// <summary>Valor gravado em pagamentos.provedor. Conciliacao historica depende dele.</summary>
    public const string NomeProvedor = "infinitepay";

    /// <summary>
    /// Contrato com a camada Application (PagamentoService le estes literais em StatusOriginal):
    /// o gateway respondeu e nao conhece a transacao. Aviso provavelmente forjado.
    /// </summary>
    public const string StatusNaoEncontrado = "nao-encontrado";

    /// <summary>
    /// Contrato com a camada Application: nao conseguimos falar com o gateway. E INCONCLUSIVO —
    /// nunca reprovar nem aprovar por causa disto; o evento fica para nova tentativa.
    /// </summary>
    public const string StatusFalhaTransporte = "falha-de-transporte";

    /// <summary>Corpo cru truncado antes de virar log ou coluna jsonb.</summary>
    private const int LimiteRawCaracteres = 8000;

    /// <summary>Nomes de propriedade onde a resposta pode esconder o objeto util.</summary>
    private static readonly string[] ObjetosAninhados =
        ["data", "transaction", "payment", "invoice", "checkout", "result", "order"];

    private static readonly JsonSerializerOptions OpcoesSerializacao = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly InfinitePayOptions _opcoes;
    private readonly AppOptions _app;
    private readonly IClock _relogio;
    private readonly ILogger<InfinitePayGateway> _logger;

    public InfinitePayGateway(
        HttpClient http,
        IOptions<InfinitePayOptions> opcoes,
        IOptions<AppOptions> app,
        IClock relogio,
        ILogger<InfinitePayGateway> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _opcoes = opcoes?.Value ?? throw new ArgumentNullException(nameof(opcoes));
        _app = app?.Value ?? throw new ArgumentNullException(nameof(app));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Nome => NomeProvedor;

    /// <inheritdoc />
    public async Task<CheckoutCriadoInfo> CriarCheckoutAsync(
        CheckoutRequisicaoInfo requisicao,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        if (string.IsNullOrWhiteSpace(_opcoes.Handle))
            return CheckoutCriadoInfo.Falha("InfinitePay:Handle nao configurado.");

        // Guarda contra a falha #3 do repo de referencia. O order_nsu previsivel deixa qualquer
        // um forjar um retorno de pagamento para pedido alheio; um nsu puramente numerico so
        // chega aqui se alguem tiver trocado a geracao do CheckoutService por um sequencial.
        if (string.IsNullOrWhiteSpace(requisicao.OrderNsu))
            return CheckoutCriadoInfo.Falha("OrderNsu nao informado.");

        if (requisicao.OrderNsu.All(char.IsDigit))
            return CheckoutCriadoInfo.Falha(
                "OrderNsu sequencial nao e aceito: use um identificador nao enumeravel.");

        if (requisicao.Itens.Count == 0)
            return CheckoutCriadoInfo.Falha("Cobranca sem itens.");

        var somaItens = requisicao.Itens.Sum(item => item.TotalCentavos);

        // O total do pedido e o que o cliente ve e o que sera conferido no payment_check. Se a
        // soma das linhas divergir, a conferencia de valor NUNCA fecharia e todo pedido cairia em
        // revisao manual. Falhar aqui e barato; descobrir depois do cliente pagar, nao.
        if (somaItens != requisicao.TotalCentavos)
            return CheckoutCriadoInfo.Falha(
                $"Soma das linhas ({somaItens}) diverge do total informado ({requisicao.TotalCentavos}).");

        var payload = new InfinitePayCheckoutRequest
        {
            Handle = _opcoes.Handle.Trim().TrimStart('@'),
            OrderNsu = requisicao.OrderNsu,
            RedirectUrl = requisicao.UrlRetorno,
            WebhookUrl = requisicao.UrlWebhook,
            Items = [.. requisicao.Itens.Select(item => new InfinitePayItem
            {
                Quantity = item.Quantidade,
                // Centavos inteiros. Nenhuma multiplicacao por 100 acontece aqui: dinheiro ja
                // chega em centavos desde o dominio, e e justamente essa conversao no ultimo
                // instante que produz o classico erro de um centavo.
                Price = item.PrecoUnitarioCentavos,
                Description = Truncar(item.Descricao, 120)
            })],
            Customer = MontarCliente(requisicao.Cliente)
        };

        string? corpo = null;

        try
        {
            using var conteudo = new StringContent(
                JsonSerializer.Serialize(payload, OpcoesSerializacao),
                Encoding.UTF8,
                "application/json");

            using var resposta = await _http.PostAsync(Url(_opcoes.CheckoutPath), conteudo, ct);

            corpo = await resposta.Content.ReadAsStringAsync(ct);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "InfinitePay recusou a criacao do checkout. Status={Status} OrderNsu={OrderNsu} Corpo={Corpo}",
                    (int)resposta.StatusCode,
                    requisicao.OrderNsu,
                    Truncar(corpo, LimiteRawCaracteres));

                return CheckoutCriadoInfo.Falha(
                    $"InfinitePay respondeu HTTP {(int)resposta.StatusCode} na criacao do checkout.",
                    Truncar(corpo, LimiteRawCaracteres));
            }

            using var documento = JsonDocument.Parse(corpo);
            var raiz = documento.RootElement;

            var url = LerTexto(raiz, "url", "checkout_url", "payment_url", "link");

            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogError(
                    "InfinitePay respondeu 200 sem URL de checkout. OrderNsu={OrderNsu} Corpo={Corpo}",
                    requisicao.OrderNsu,
                    Truncar(corpo, LimiteRawCaracteres));

                return CheckoutCriadoInfo.Falha(
                    "InfinitePay nao devolveu a URL do checkout.",
                    Truncar(corpo, LimiteRawCaracteres));
            }

            return new CheckoutCriadoInfo
            {
                Sucesso = true,
                UrlCheckout = url,
                OrderNsu = LerTexto(raiz, "order_nsu") ?? requisicao.OrderNsu,
                ProviderChargeId = LerTexto(raiz, "id", "invoice_id", "slug", "transaction_nsu"),
                QrCodePix = LerTexto(raiz, "pix_code", "qr_code", "copy_paste"),
                LinhaDigitavel = LerTexto(raiz, "barcode", "digitable_line"),
                // A InfinitePay nao devolve validade do link. O prazo e nosso e vem das options:
                // e ele que o worker de expiracao usa para devolver a reserva de estoque.
                ExpiraEmUtc = _relogio.UtcNow.AddMinutes(_opcoes.ExpiracaoMinutos),
                RawJson = Truncar(corpo, LimiteRawCaracteres)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelamento do chamador nao e falha do parceiro: propaga para o checkout abortar.
            throw;
        }
        catch (Exception excecao)
        {
            _logger.LogError(
                excecao,
                "Falha de comunicacao com a InfinitePay ao criar checkout. OrderNsu={OrderNsu}",
                requisicao.OrderNsu);

            return CheckoutCriadoInfo.Falha(
                "Nao foi possivel falar com o provedor de pagamento.",
                corpo is null ? null : Truncar(corpo, LimiteRawCaracteres));
        }
    }

    /// <inheritdoc />
    public async Task<ConsultaPagamentoInfo> ConsultarPagamentoAsync(
        string orderNsu,
        string? transactionNsu = null,
        string? slug = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderNsu))
            return NaoEncontrado(StatusNaoEncontrado, null);

        var consulta = new StringBuilder(Url(_opcoes.PaymentCheckPath));
        consulta.Append("?handle=").Append(Uri.EscapeDataString(_opcoes.Handle.Trim().TrimStart('@')));
        consulta.Append("&order_nsu=").Append(Uri.EscapeDataString(orderNsu));

        if (!string.IsNullOrWhiteSpace(transactionNsu))
            consulta.Append("&transaction_nsu=").Append(Uri.EscapeDataString(transactionNsu));

        if (!string.IsNullOrWhiteSpace(slug))
            consulta.Append("&slug=").Append(Uri.EscapeDataString(slug));

        string? corpo = null;

        try
        {
            using var resposta = await _http.GetAsync(consulta.ToString(), ct);

            corpo = await resposta.Content.ReadAsStringAsync(ct);

            if (resposta.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "InfinitePay nao conhece a transacao. OrderNsu={OrderNsu}",
                    orderNsu);

                return NaoEncontrado(StatusNaoEncontrado, Truncar(corpo, LimiteRawCaracteres));
            }

            if (!resposta.IsSuccessStatusCode)
            {
                // 4xx/5xx que nao seja 404 e indefinicao, nao negativa. Tratar como "nao pagou"
                // aqui seria o caminho para cancelar pedido de cliente que pagou.
                _logger.LogError(
                    "InfinitePay respondeu HTTP {Status} na conferencia. OrderNsu={OrderNsu} Corpo={Corpo}",
                    (int)resposta.StatusCode,
                    orderNsu,
                    Truncar(corpo, LimiteRawCaracteres));

                return NaoEncontrado(StatusFalhaTransporte, Truncar(corpo, LimiteRawCaracteres));
            }

            using var documento = JsonDocument.Parse(corpo);
            var raiz = Desembrulhar(documento.RootElement);

            var statusTexto = LerTexto(raiz, "status", "payment_status", "transaction_status", "state");
            var pago = LerBooleano(raiz, "paid", "success", "captured", "is_paid", "approved");

            var status = MapearStatus(statusTexto, pago);

            var valorCentavos = LerCentavos(
                raiz, "paid_amount", "captured_amount", "amount", "value", "total", "price");

            var encontrado = statusTexto is not null || pago is not null || valorCentavos is not null;

            if (!encontrado)
            {
                _logger.LogWarning(
                    "Conferencia da InfinitePay sem campo reconhecivel. OrderNsu={OrderNsu} Corpo={Corpo}",
                    orderNsu,
                    Truncar(corpo, LimiteRawCaracteres));

                return NaoEncontrado(StatusNaoEncontrado, Truncar(corpo, LimiteRawCaracteres));
            }

            if (status == StatusPagamentoGateway.Desconhecido)
            {
                // Status novo inventado pelo provedor. Fica registrado cru para auditoria e o
                // servico NAO aprova — nao decidir e melhor que decidir errado a favor de quem paga.
                _logger.LogWarning(
                    "Status desconhecido da InfinitePay. OrderNsu={OrderNsu} StatusOriginal={Status}",
                    orderNsu,
                    statusTexto);
            }

            return new ConsultaPagamentoInfo
            {
                Encontrado = true,
                Status = status,
                ValorCentavos = valorCentavos,
                Metodo = LerTexto(raiz, "capture_method", "payment_method", "method") ?? slug,
                Parcelas = LerInteiro(raiz, "installments", "installment_count", "installment_quantity"),
                OrderNsu = LerTexto(raiz, "order_nsu") ?? orderNsu,
                TransactionNsu = LerTexto(raiz, "transaction_nsu", "nsu", "transaction_id") ?? transactionNsu,
                UrlComprovante = LerTexto(raiz, "receipt_url", "receipt"),
                PagoEmUtc = LerData(raiz, "paid_at", "captured_at", "created_at"),
                StatusOriginal = statusTexto,
                RawJson = Truncar(corpo, LimiteRawCaracteres)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            _logger.LogError(
                excecao,
                "Falha de comunicacao com a InfinitePay na conferencia. OrderNsu={OrderNsu}",
                orderNsu);

            return NaoEncontrado(StatusFalhaTransporte, corpo is null ? null : Truncar(corpo, LimiteRawCaracteres));
        }
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private string Url(string caminho) =>
        $"{_opcoes.BaseUrl.TrimEnd('/')}/{(caminho ?? string.Empty).TrimStart('/')}";

    private InfinitePayCustomer? MontarCliente(CheckoutClienteInfo? cliente)
    {
        if (cliente is null)
            return null;

        if (string.IsNullOrWhiteSpace(cliente.Nome)
            && string.IsNullOrWhiteSpace(cliente.Email)
            && string.IsNullOrWhiteSpace(cliente.Telefone))
        {
            return null;
        }

        // O documento do cliente NAO e enviado: a InfinitePay nao pede CPF no link de checkout e
        // mandar dado pessoal que o parceiro nao precisa e superficie de vazamento de graca.
        return new InfinitePayCustomer
        {
            Name = cliente.Nome,
            Email = cliente.Email,
            PhoneNumber = cliente.Telefone
        };
    }

    private static ConsultaPagamentoInfo NaoEncontrado(string statusOriginal, string? raw) =>
        new()
        {
            Encontrado = false,
            Status = StatusPagamentoGateway.Desconhecido,
            StatusOriginal = statusOriginal,
            RawJson = raw
        };

    /// <summary>
    /// Alguns provedores embrulham o resultado em "data"/"transaction". Descer um nivel evita
    /// que a leitura falhe silenciosamente e o pedido fique eternamente pendente.
    /// </summary>
    private static JsonElement Desembrulhar(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object)
            return raiz;

        // Se a raiz ja tem status ou amount, e ela mesma.
        if (TemAlguma(raiz, "status", "paid", "amount", "payment_status"))
            return raiz;

        foreach (var nome in ObjetosAninhados)
        {
            if (raiz.TryGetProperty(nome, out var filho) && filho.ValueKind == JsonValueKind.Object)
                return filho;
        }

        return raiz;
    }

    private static bool TemAlguma(JsonElement elemento, params string[] nomes) =>
        nomes.Any(nome => elemento.TryGetProperty(nome, out _));

    private static bool TentarObter(JsonElement elemento, string nome, out JsonElement valor)
    {
        valor = default;

        if (elemento.ValueKind != JsonValueKind.Object)
            return false;

        if (!elemento.TryGetProperty(nome, out var encontrado))
            return false;

        if (encontrado.ValueKind == JsonValueKind.Null)
            return false;

        valor = encontrado;
        return true;
    }

    private static string? LerTexto(JsonElement elemento, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(elemento, nome, out var valor))
                continue;

            var texto = valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.Number => valor.ToString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(texto))
                return texto;
        }

        return null;
    }

    private static bool? LerBooleano(JsonElement elemento, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(elemento, nome, out var valor))
                continue;

            switch (valor.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.String when bool.TryParse(valor.GetString(), out var convertido):
                    return convertido;
            }
        }

        return null;
    }

    private static int? LerInteiro(JsonElement elemento, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(elemento, nome, out var valor))
                continue;

            if (valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero))
                return numero;

            if (valor.ValueKind == JsonValueKind.String
                && int.TryParse(valor.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var texto))
            {
                return texto;
            }
        }

        return null;
    }

    /// <summary>
    /// Le valor monetario tolerando as duas formas que o provedor usa: inteiro em CENTAVOS
    /// (12990) e decimal em REAIS (129.90).
    ///
    /// O criterio e a presenca de casas decimais, nao a magnitude. Chutar por magnitude
    /// ("numero pequeno deve ser reais") transformaria um pedido de R$ 1,50 em R$ 150,00 na
    /// conferencia — e a conferencia e justamente o que impede aprovar valor errado.
    /// </summary>
    private static int? LerCentavos(JsonElement elemento, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(elemento, nome, out var valor))
                continue;

            var cru = valor.ValueKind switch
            {
                JsonValueKind.Number => valor.GetRawText(),
                JsonValueKind.String => valor.GetString(),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(cru))
                continue;

            // Conversao malsucedida NAO encerra a busca: o provedor pode trazer "amount" nulo e
            // o valor bom em "captured_amount". Desistir no primeiro nome deixaria a conferencia
            // de valor sem numero para comparar, e o pedido cairia em revisao manual a toa.
            var centavos = ConverterParaCentavos(cru);

            if (centavos is not null)
                return centavos;
        }

        return null;
    }

    private static int? ConverterParaCentavos(string cru)
    {
        var texto = cru.Trim();

        if (!texto.Contains('.') && !texto.Contains(','))
        {
            return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inteiro)
                && inteiro is >= int.MinValue and <= int.MaxValue
                ? (int)inteiro
                : null;
        }

        // Reais decimais: o ponto e o separador do JSON; virgula so aparece quando o provedor
        // manda texto ja formatado.
        var normalizado = texto.Replace(",", ".", StringComparison.Ordinal);

        if (!decimal.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var reais))
            return null;

        var centavos = Math.Round(reais * 100m, MidpointRounding.AwayFromZero);

        if (centavos < int.MinValue || centavos > int.MaxValue)
            return null;

        return (int)centavos;
    }

    private static DateTime? LerData(JsonElement elemento, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(elemento, nome, out var valor) || valor.ValueKind != JsonValueKind.String)
                continue;

            if (DateTimeOffset.TryParse(
                    valor.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var data))
            {
                return data.UtcDateTime;
            }
        }

        return null;
    }

    /// <summary>
    /// Traducao do status do provedor. Lista FECHADA: qualquer coisa fora dela vira Desconhecido,
    /// e Desconhecido nunca aprova pedido.
    /// </summary>
    private static StatusPagamentoGateway MapearStatus(string? statusOriginal, bool? pago)
    {
        var normalizado = (statusOriginal ?? string.Empty).Trim().ToLowerInvariant();

        var mapeado = normalizado switch
        {
            "paid" or "approved" or "succeeded" or "success" or "captured" or "confirmed" or "completed"
                => StatusPagamentoGateway.Aprovado,

            "pending" or "waiting" or "waiting_payment" or "created" or "processing" or "authorized" or "in_process"
                => StatusPagamentoGateway.Pendente,

            "refused" or "declined" or "denied" or "rejected" or "failed" or "error" or "not_authorized"
                => StatusPagamentoGateway.Recusado,

            "expired" or "timeout"
                => StatusPagamentoGateway.Expirado,

            "canceled" or "cancelled" or "voided" or "void"
                => StatusPagamentoGateway.Cancelado,

            "refunded" or "reversed" or "chargeback" or "charged_back"
                => StatusPagamentoGateway.Estornado,

            _ => StatusPagamentoGateway.Desconhecido
        };

        if (mapeado != StatusPagamentoGateway.Desconhecido)
            return mapeado;

        // Sem status textual util, o booleano explicito ainda serve: "paid": false e uma negativa
        // clara do provedor, nao ausencia de informacao.
        return pago switch
        {
            true => StatusPagamentoGateway.Aprovado,
            false => StatusPagamentoGateway.Pendente,
            _ => StatusPagamentoGateway.Desconhecido
        };
    }

    private static string Truncar(string? valor, int limite)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        return valor.Length <= limite ? valor : valor[..limite];
    }
}
