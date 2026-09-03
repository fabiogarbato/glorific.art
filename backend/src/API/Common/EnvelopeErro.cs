using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glorific.Api.Common;

/// <summary>
/// ENVELOPE UNICO de erro da API: { statusCode, error, traceId, errors? }.
///
/// "Unico" e a palavra importante. No repo de referencia conviviam quatro formatos — o do
/// middleware, o { message } do AuthController, o ValidationProblemDetails do [ApiController] e
/// o corpo VAZIO de 401/403 do JwtBearer. O front acabou com tres ramos de parse de erro e o
/// 401 nao exibia mensagem nenhuma.
///
/// Aqui todos os caminhos passam por este record: middleware de excecao, validacao de
/// ModelState, challenge/forbidden do JWT e rejeicao do rate limiter.
/// </summary>
public sealed record EnvelopeErro
{
    public required int StatusCode { get; init; }

    /// <summary>Mensagem pronta para o usuario final. E o campo que o front le.</summary>
    public required string Error { get; init; }

    /// <summary>Correlaciona a resposta com o log do servidor.</summary>
    public required string TraceId { get; init; }

    /// <summary>Campo -> mensagens. Omitido do JSON quando nao ha detalhe por campo.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}

/// <summary>Fabrica e escrita do envelope. Todo caminho de erro da API entra por aqui.</summary>
public static class RespostaErro
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        // Sem isso o envelope de um erro simples sai com "errors": null pendurado.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// TraceId de verdade: o id do Activity corrente quando ha tracing distribuido, e o
    /// TraceIdentifier do Kestrel como fallback. E o mesmo valor que aparece no log.
    /// </summary>
    public static string TraceId(HttpContext contexto) =>
        Activity.Current?.Id ?? contexto.TraceIdentifier;

    public static EnvelopeErro Criar(
        HttpContext contexto,
        int statusCode,
        string mensagem,
        IReadOnlyDictionary<string, string[]>? erros = null) =>
        new()
        {
            StatusCode = statusCode,
            Error = mensagem,
            TraceId = TraceId(contexto),
            Errors = erros is { Count: > 0 } ? erros : null
        };

    /// <summary>
    /// Escreve o envelope na resposta.
    ///
    /// A guarda de HasStarted nao e paranoia: o OnChallenge do JwtBearer e o OnRejected do rate
    /// limiter podem rodar depois de outro componente ja ter comecado a resposta, e escrever ali
    /// lanca uma segunda excecao que substitui a original no log.
    /// </summary>
    public static async Task EscreverAsync(
        HttpContext contexto,
        int statusCode,
        string mensagem,
        IReadOnlyDictionary<string, string[]>? erros = null)
    {
        if (contexto.Response.HasStarted)
            return;

        contexto.Response.Clear();
        contexto.Response.StatusCode = statusCode;
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var envelope = Criar(contexto, statusCode, mensagem, erros);

        await contexto.Response.WriteAsync(
            JsonSerializer.Serialize(envelope, OpcoesJson),
            contexto.RequestAborted);
    }
}
