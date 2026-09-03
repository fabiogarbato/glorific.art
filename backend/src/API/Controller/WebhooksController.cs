using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Glorific.Api.Configuration;
using Glorific.Application.Models.Pagamento;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Glorific.Api.Controller;

/// <summary>
/// Entrada dos avisos de pagamento.
///
/// [AllowAnonymous] EXPLICITO: quem chama aqui e o gateway (server-to-server) e o navegador do
/// cliente voltando do checkout — nenhum dos dois tem token nosso.
///
/// E o ponto mais exposto da API, entao vale escrever o que ele NAO faz: nao confia em nada do
/// que recebe. O corpo do webhook da InfinitePay nao tem assinatura e o retorno e um GET que
/// qualquer pessoa monta. Este controller apenas EXTRAI a identificacao da transacao e delega; a
/// decisao sai da conferencia server-to-server feita no PagamentoService.
///
/// Responde 200 em quase todo caminho de proposito: gateway que recebe 4xx/5xx reentrega em loop,
/// e reentrega de um aviso que ja entendemos so gera carga. 400 fica reservado para corpo que nem
/// da para interpretar.
/// </summary>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
[Route("api/v1/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    /// <summary>
    /// Teto do corpo aceito. Endpoint publico sem autenticacao nao pode aceitar payload de
    /// tamanho arbitrario — e o jeito mais barato de consumir memoria do servidor.
    /// </summary>
    private const int LimiteCorpoBytes = 64 * 1024;

    private readonly IPagamentoService _pagamentos;
    private readonly AppOptions _app;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IPagamentoService pagamentos,
        IOptions<AppOptions> app,
        ILogger<WebhooksController> logger)
    {
        _pagamentos = pagamentos ?? throw new ArgumentNullException(nameof(pagamentos));
        _app = app?.Value ?? throw new ArgumentNullException(nameof(app));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// POST do gateway. Corpo esperado: order_nsu, transaction_nsu, capture_method, amount,
    /// receipt_url.
    ///
    /// O amount que chega aqui e usado APENAS para log. Quem decide valor e a conferencia.
    /// </summary>
    [HttpPost("pagamento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pagamento(CancellationToken cancellationToken)
    {
        var corpo = await LerCorpoAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(corpo))
            return BadRequest(new { received = false, error = "Corpo vazio." });

        JsonDocument documento;

        try
        {
            documento = JsonDocument.Parse(corpo);
        }
        catch (JsonException)
        {
            // Corpo que nao e JSON nao pode nem ser gravado em jsonb para auditoria.
            _logger.LogWarning("Webhook de pagamento com corpo nao-JSON recebido e descartado.");
            return BadRequest(new { received = false, error = "Corpo invalido." });
        }

        using (documento)
        {
            var raiz = documento.RootElement;

            var orderNsu = Texto(raiz, "order_nsu", "orderNsu");

            if (string.IsNullOrWhiteSpace(orderNsu))
                return BadRequest(new { received = false, error = "order_nsu ausente." });

            var aviso = new WebhookPagamentoInfo
            {
                OrderNsu = orderNsu,
                TransactionNsu = Texto(raiz, "transaction_nsu", "transactionNsu"),
                Slug = Texto(raiz, "capture_method", "slug"),
                ValorAnunciadoCentavos = Inteiro(raiz, "amount"),
                UrlComprovante = Texto(raiz, "receipt_url", "receiptUrl"),
                // Sem id de evento nativo, o identificador e derivado do CORPO INTEIRO: reentrega
                // identica colide na unique e vira 200; mudanca de status (pendente para pago)
                // gera corpo diferente e, portanto, um evento novo que precisa ser processado.
                ProviderEventId = DerivarEventId("webhook", corpo),
                Payload = corpo
            };

            var resultado = await _pagamentos.ReceberAvisoAsync(aviso, cancellationToken);

            _logger.LogInformation(
                "Webhook de pagamento processado. OrderNsu={OrderNsu} Resultado={Resultado}",
                orderNsu,
                resultado);

            return Ok(new { received = true });
        }
    }

    /// <summary>
    /// Retorno do NAVEGADOR do cliente depois do checkout hospedado.
    ///
    /// E tratado exatamente como o webhook — inclusive na desconfianca: e uma URL GET que qualquer
    /// um monta. Serve para adiantar a confirmacao quando o webhook demora, nunca como prova.
    /// Termina em redirect para a loja porque quem esta do outro lado e uma pessoa, nao um servidor.
    /// </summary>
    [HttpGet("pagamento/retorno")]
    [EnableRateLimiting(PoliticasRateLimit.Consulta)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> RetornoPagamento(
        [FromQuery(Name = "order_nsu")] string? orderNsu,
        [FromQuery(Name = "transaction_nsu")] string? transactionNsu,
        [FromQuery(Name = "capture_method")] string? captureMethod,
        [FromQuery] string? slug,
        [FromQuery(Name = "receipt_url")] string? receiptUrl,
        CancellationToken cancellationToken)
    {
        var resultado = ResultadoAvisoPagamento.PagamentoNaoEncontrado;

        if (!string.IsNullOrWhiteSpace(orderNsu))
        {
            // Payload sintetico e valido como JSON: a coluna de auditoria e jsonb e precisa
            // guardar o que chegou, mesmo quando "o que chegou" foi uma query string.
            var payload = JsonSerializer.Serialize(new
            {
                origem = "retorno",
                order_nsu = orderNsu,
                transaction_nsu = transactionNsu,
                capture_method = captureMethod,
                slug,
                receipt_url = receiptUrl
            });

            var aviso = new WebhookPagamentoInfo
            {
                OrderNsu = orderNsu,
                TransactionNsu = transactionNsu,
                Slug = slug ?? captureMethod,
                UrlComprovante = receiptUrl,
                ProviderEventId = DerivarEventId("retorno", payload),
                Payload = payload
            };

            resultado = await _pagamentos.ReceberAvisoAsync(aviso, cancellationToken);
        }

        // Nenhum dado do cliente vai na URL: so o desfecho, que o front usa para escolher a tela.
        return Redirect(_app.UrlLoja($"checkout/retorno?resultado={resultado}"));
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private async Task<string?> LerCorpoAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > LimiteCorpoBytes)
        {
            _logger.LogWarning(
                "Webhook de pagamento recusado por tamanho: {Tamanho} bytes.",
                Request.ContentLength);

            return null;
        }

        using var leitor = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);

        var buffer = new char[LimiteCorpoBytes];
        var lidos = await leitor.ReadBlockAsync(buffer, cancellationToken);

        return new string(buffer, 0, lidos);
    }

    /// <summary>
    /// Identificador estavel do evento. E o valor que a unique em pagamentos_eventos usa para
    /// transformar reentrega em 200 imediato, e por isso ele TEM que ser funcao do conteudo:
    /// derivar so do order_nsu faria a segunda notificacao (a que anuncia o pagamento) ser
    /// descartada como duplicata da primeira.
    /// </summary>
    private static string DerivarEventId(string origem, string conteudo)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));

        return $"{origem}:{Convert.ToHexString(digest)}";
    }

    private static string? Texto(JsonElement raiz, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!raiz.TryGetProperty(nome, out var valor))
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

    private static int? Inteiro(JsonElement raiz, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (raiz.TryGetProperty(nome, out var valor)
                && valor.ValueKind == JsonValueKind.Number
                && valor.TryGetInt32(out var numero))
            {
                return numero;
            }
        }

        return null;
    }
}
