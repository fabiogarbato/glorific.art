using System.Net;
using System.Text.Json;
using Xunit;

namespace Glorific.Tests.TestSupport;

/// <summary>Envelope de erro da API, ja lido do corpo: { statusCode, error, traceId, errors? }.</summary>
public sealed record EnvelopeLido(
    int StatusCode,
    string Error,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool TemDetalhePorCampo => Errors is { Count: > 0 };
}

/// <summary>
/// Leitura e conferencia do ENVELOPE UNICO de erro.
///
/// Existe porque "sai no mesmo formato" e uma afirmacao que precisa ser verificada em TODOS os
/// caminhos, e nao so no feliz. No repo de referencia conviviam quatro formatos — o do
/// middleware, o { message } do AuthController, o ValidationProblemDetails do [ApiController] e o
/// corpo VAZIO de 401/403 do JwtBearer — e o front acabou com tres ramos de parse de erro.
/// </summary>
public static class EnvelopeHttp
{
    /// <summary>
    /// Confere o contrato inteiro de uma resposta de erro e devolve o envelope lido.
    ///
    /// Confere, alem do status: content-type JSON, corpo NAO VAZIO, o campo statusCode igual ao
    /// status HTTP de verdade (divergir entre os dois faz o front decidir pelo campo errado) e
    /// error/traceId preenchidos.
    /// </summary>
    public static async Task<EnvelopeLido> AssertPadraoAsync(
        HttpResponseMessage resposta,
        HttpStatusCode esperado)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        var rota = $"{resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri?.PathAndQuery}";
        var texto = await resposta.Content.ReadAsStringAsync();

        Assert.True(
            esperado == resposta.StatusCode,
            $"{rota}: esperado {(int)esperado}, veio {(int)resposta.StatusCode}. Corpo: {texto}");

        Assert.False(
            string.IsNullOrWhiteSpace(texto),
            $"{rota}: respondeu {(int)resposta.StatusCode} com CORPO VAZIO — o front nao tem o que exibir.");

        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);

        var envelope = Ler(texto, rota);

        Assert.Equal((int)esperado, envelope.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Error), $"{rota}: campo 'error' vazio.");
        Assert.False(string.IsNullOrWhiteSpace(envelope.TraceId), $"{rota}: campo 'traceId' vazio.");

        return envelope;
    }

    public static async Task<EnvelopeLido> LerAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        var rota = $"{resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri?.PathAndQuery}";

        return Ler(await resposta.Content.ReadAsStringAsync(), rota);
    }

    private static EnvelopeLido Ler(string texto, string rota)
    {
        using var documento = JsonDocument.Parse(texto);
        var raiz = documento.RootElement;

        Assert.Equal(JsonValueKind.Object, raiz.ValueKind);

        Assert.True(raiz.TryGetProperty("statusCode", out var statusCode), $"{rota}: sem 'statusCode'.");
        Assert.True(raiz.TryGetProperty("error", out var error), $"{rota}: sem 'error'.");
        Assert.True(raiz.TryGetProperty("traceId", out var traceId), $"{rota}: sem 'traceId'.");

        Dictionary<string, string[]>? erros = null;

        if (raiz.TryGetProperty("errors", out var detalhe) && detalhe.ValueKind == JsonValueKind.Object)
        {
            erros = new Dictionary<string, string[]>(StringComparer.Ordinal);

            foreach (var campo in detalhe.EnumerateObject())
            {
                erros[campo.Name] = campo.Value.ValueKind == JsonValueKind.Array
                    ? campo.Value.EnumerateArray().Select(m => m.GetString() ?? string.Empty).ToArray()
                    : Array.Empty<string>();
            }
        }

        return new EnvelopeLido(
            statusCode.GetInt32(),
            error.GetString() ?? string.Empty,
            traceId.GetString() ?? string.Empty,
            erros);
    }
}
