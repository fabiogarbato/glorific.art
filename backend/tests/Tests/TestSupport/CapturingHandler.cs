using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Glorific.Tests.TestSupport;

/// <summary>
/// Test double de <see cref="HttpMessageHandler"/> feito a mao (o projeto nao usa biblioteca de
/// mock de proposito).
///
/// Existe para permitir a asserção nos DOIS lados do fio de uma integracao externa:
///
/// - o que foi ENVIADO (metodo, caminho, query, headers, corpo JSON), capturado em
///   <see cref="Requisicoes"/> ANTES de o HttpClient descartar o conteudo da requisicao;
/// - o que o adaptador FEZ com a resposta canned devolvida aqui.
///
/// Nenhum byte sai da maquina: o handler substitui o transporte inteiro, entao os testes podem
/// apontar para hosts ficticios (".invalid") sem nunca resolver DNS.
///
/// Quando ha menos respostas canned que chamadas, a ULTIMA resposta e repetida. E o que permite
/// escrever "dois checkouts seguidos" com uma resposta so e ainda assim inspecionar as duas
/// requisicoes enviadas.
/// </summary>
public sealed class CapturingHandler : HttpMessageHandler
{
    private readonly object _trava = new();
    private readonly List<RequisicaoCapturada> _requisicoes = [];
    private readonly List<Func<HttpResponseMessage>> _respostas;
    private int _chamadas;

    private CapturingHandler(IEnumerable<Func<HttpResponseMessage>> respostas)
    {
        _respostas = new List<Func<HttpResponseMessage>>(respostas);

        if (_respostas.Count == 0)
            throw new ArgumentException("Informe ao menos uma resposta canned.", nameof(respostas));
    }

    /// <summary>Responde sempre com o mesmo corpo JSON e o mesmo status.</summary>
    public static CapturingHandler ComJson(HttpStatusCode status, string corpoJson) =>
        new(new Func<HttpResponseMessage>[] { () => Resposta(status, corpoJson, "application/json") });

    /// <summary>Responde 200 com o corpo JSON informado.</summary>
    public static CapturingHandler ComJsonOk(string corpoJson) =>
        ComJson(HttpStatusCode.OK, corpoJson);

    /// <summary>
    /// Responde com corpo VAZIO — e o que o microservico do Melhor Envio faz no 401 (challenge do
    /// ApiKeyAuthenticationHandler, sem ProblemDetails).
    /// </summary>
    public static CapturingHandler ComCorpoVazio(HttpStatusCode status) =>
        new(new Func<HttpResponseMessage>[] { () => Resposta(status, string.Empty, "text/plain") });

    /// <summary>Responde com um corpo de tipo arbitrario (HTML de proxy, texto de gateway...).</summary>
    public static CapturingHandler ComTexto(HttpStatusCode status, string corpo, string contentType) =>
        new(new Func<HttpResponseMessage>[] { () => Resposta(status, corpo, contentType) });

    /// <summary>
    /// Uma resposta canned por chamada, na ordem. Depois de esgotada a lista a ultima se repete.
    /// </summary>
    public static CapturingHandler ComSequencia(params (HttpStatusCode Status, string Corpo)[] respostas)
    {
        ArgumentNullException.ThrowIfNull(respostas);

        var fabricas = new List<Func<HttpResponseMessage>>(respostas.Length);

        foreach (var (status, corpo) in respostas)
            fabricas.Add(() => Resposta(status, corpo, "application/json"));

        return new CapturingHandler(fabricas);
    }

    /// <summary>
    /// Falha de transporte: nao ha resposta nenhuma.
    ///
    /// A fabrica devolve uma excecao NOVA a cada chamada de proposito — reaproveitar a mesma
    /// instancia entre chamadas empilharia stack trace e tornaria o teste dependente de ordem.
    /// Para simular TIMEOUT do HttpClient use <see cref="ComoTimeout"/>: o adaptador distingue
    /// timeout de cancelamento do chamador pelo CancellationToken, nao pelo tipo da excecao.
    /// </summary>
    public static CapturingHandler QueLanca(Func<Exception> fabricaDeExcecao)
    {
        ArgumentNullException.ThrowIfNull(fabricaDeExcecao);

        return new CapturingHandler(new Func<HttpResponseMessage>[] { () => throw fabricaDeExcecao() });
    }

    /// <summary>
    /// Reproduz exatamente o timeout do <see cref="HttpClient"/>: TaskCanceledException com
    /// TimeoutException dentro e SEM cancelamento do token do chamador.
    /// </summary>
    public static CapturingHandler ComoTimeout() =>
        QueLanca(() => new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.",
            new TimeoutException()));

    /// <summary>Tudo que foi enviado, na ordem.</summary>
    public IReadOnlyList<RequisicaoCapturada> Requisicoes
    {
        get
        {
            lock (_trava)
                return _requisicoes.ToArray();
        }
    }

    /// <summary>Quantas vezes o adaptador foi ao fio. Zero prova que ele barrou antes de sair.</summary>
    public int Chamadas
    {
        get
        {
            lock (_trava)
                return _chamadas;
        }
    }

    /// <summary>
    /// A unica requisicao enviada. Lanca quando houve zero ou mais de uma — a contagem faz parte
    /// da asserção: um adaptador que chama o parceiro duas vezes por engano nao pode passar.
    /// </summary>
    public RequisicaoCapturada Unica
    {
        get
        {
            lock (_trava)
            {
                return _requisicoes.Count == 1
                    ? _requisicoes[0]
                    : throw new InvalidOperationException(
                        $"Esperava exatamente 1 requisicao enviada, mas foram {_requisicoes.Count}.");
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Um handler real honra o token antes de qualquer trabalho. Sem isto o cancelamento do
        // chamador nao seria observavel em teste, e o adaptador parece tratar cancelamento e
        // timeout do mesmo jeito — que e justamente a distincao que precisa ficar provada.
        cancellationToken.ThrowIfCancellationRequested();

        // O corpo e lido AQUI, e nao depois do teste: o HttpClient descarta o Content assim que a
        // chamada termina, e a asserção sobre o payload enviado ficaria olhando para um stream morto.
        var capturada = await RequisicaoCapturada.DeAsync(request, cancellationToken);

        Func<HttpResponseMessage> fabrica;

        lock (_trava)
        {
            _requisicoes.Add(capturada);
            fabrica = _respostas[Math.Min(_chamadas, _respostas.Count - 1)];
            _chamadas++;
        }

        return fabrica();
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string corpo, string contentType)
    {
        var conteudo = new StringContent(corpo ?? string.Empty, Encoding.UTF8);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(contentType) { CharSet = "utf-8" };

        return new HttpResponseMessage(status) { Content = conteudo };
    }
}

/// <summary>
/// Fotografia imutavel do que saiu pelo fio. Tudo ja materializado em string: nada aqui depende
/// de objeto do HttpClient que ja foi descartado.
/// </summary>
public sealed class RequisicaoCapturada
{
    private readonly Dictionary<string, string[]> _cabecalhos;
    private readonly Dictionary<string, string> _query;
    private JsonDocument? _documento;

    private RequisicaoCapturada(
        HttpMethod metodo,
        Uri? uri,
        string? corpo,
        string? contentType,
        Dictionary<string, string[]> cabecalhos,
        Dictionary<string, string> query)
    {
        Metodo = metodo;
        Uri = uri;
        Corpo = corpo;
        ContentType = contentType;
        _cabecalhos = cabecalhos;
        _query = query;
    }

    public HttpMethod Metodo { get; }

    public Uri? Uri { get; }

    /// <summary>
    /// Caminho sem query. E o que se compara com a constante de rota do adaptador.
    /// O HttpClient resolve BaseAddress + caminho relativo ANTES de chamar o handler, entao aqui
    /// a URI ja e absoluta; o ramo relativo existe so para nao explodir se isso mudar.
    /// </summary>
    public string Caminho =>
        Uri is null ? string.Empty
        : Uri.IsAbsoluteUri ? Uri.AbsolutePath
        : Uri.ToString().Split('?')[0];

    /// <summary>Query crua, sem o "?".</summary>
    public string QueryString =>
        Uri is { IsAbsoluteUri: true } ? Uri.Query.TrimStart('?') : string.Empty;

    /// <summary>Corpo cru enviado. Null em GET/DELETE sem conteudo.</summary>
    public string? Corpo { get; }

    public string? ContentType { get; }

    /// <summary>Primeiro valor do header, ou null quando ele nao foi enviado.</summary>
    public string? Cabecalho(string nome) =>
        _cabecalhos.TryGetValue(nome, out var valores) && valores.Length > 0 ? valores[0] : null;

    public bool TemCabecalho(string nome) => _cabecalhos.ContainsKey(nome);

    /// <summary>Valor JA decodificado de um parametro de query, ou null quando ausente.</summary>
    public string? ValorDaQuery(string nome) => _query.GetValueOrDefault(nome);

    public bool TemNaQuery(string nome) => _query.ContainsKey(nome);

    /// <summary>
    /// Corpo enviado parseado como JSON. Lanca com mensagem clara quando nao houve corpo — o
    /// erro tipico e assertar payload numa rota que o adaptador chama por GET.
    /// </summary>
    public JsonElement CorpoJson
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Corpo))
                throw new InvalidOperationException(
                    $"A requisicao {Metodo} {Caminho} foi enviada sem corpo — nao ha JSON para inspecionar.");

            _documento ??= JsonDocument.Parse(Corpo);
            return _documento.RootElement;
        }
    }

    internal static async Task<RequisicaoCapturada> DeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var cabecalhos = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var cabecalho in request.Headers)
            cabecalhos[cabecalho.Key] = cabecalho.Value.ToArray();

        string? corpo = null;
        string? contentType = null;

        if (request.Content is not null)
        {
            corpo = await request.Content.ReadAsStringAsync(ct);
            contentType = request.Content.Headers.ContentType?.MediaType;

            foreach (var cabecalho in request.Content.Headers)
                cabecalhos[cabecalho.Key] = cabecalho.Value.ToArray();
        }

        return new RequisicaoCapturada(
            request.Method,
            request.RequestUri,
            corpo,
            contentType,
            cabecalhos,
            ParsearQuery(request.RequestUri));
    }

    /// <summary>
    /// Parser de query escrito a mao: o projeto de testes nao referencia System.Web nem
    /// WebUtilities, e a query destas integracoes e simples (pares chave=valor).
    /// </summary>
    private static Dictionary<string, string> ParsearQuery(Uri? uri)
    {
        var resultado = new Dictionary<string, string>(StringComparer.Ordinal);

        if (uri is null || !uri.IsAbsoluteUri)
            return resultado;

        var query = uri.Query.TrimStart('?');

        if (query.Length == 0)
            return resultado;

        foreach (var par in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separador = par.IndexOf('=', StringComparison.Ordinal);

            var nome = separador < 0 ? par : par[..separador];
            var valor = separador < 0 ? string.Empty : par[(separador + 1)..];

            resultado[Uri.UnescapeDataString(nome)] = Uri.UnescapeDataString(valor);
        }

        return resultado;
    }
}
