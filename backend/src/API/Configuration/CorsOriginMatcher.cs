namespace Glorific.Api.Configuration;

/// <summary>
/// Decide se uma origem do navegador esta liberada. Classe pura, sem nada de ASP.NET, para
/// poder ser testada direto — a fronteira de CORS e exatamente o tipo de codigo que ninguem
/// consegue exercitar de verdade se so existir dentro de uma lambda no Program.cs.
///
/// Por que nao usar WithOrigins do proprio framework: ele nao interpreta "*", e a loja precisa
/// liberar previews de deploy (https://algo.glorific.pages.dev) sem listar cada uma. Dai
/// SetIsOriginAllowed com este matcher.
///
/// Regras, e o porque de cada uma:
/// - Curinga SO no primeiro rotulo: "https://*.glorific.art" vale, "https://*.*.art" nao.
///   Curinga no meio do host abriria combinacoes que ninguem consegue revisar.
/// - Host base PRECISA ter ponto: "https://*.art" e "https://*.com" sao recusados. Sem essa
///   regra, um sufixo publico inteiro entraria na lista.
/// - Comparacao por rotulo, nunca por EndsWith solto: "https://evilglorific.art" NAO casa com
///   "https://*.glorific.art", porque a comparacao exige o ponto separador.
/// - Scheme e porta entram na comparacao: http e https sao origens distintas para o navegador,
///   e liberar :443 nao pode liberar :8443.
/// </summary>
public sealed class CorsOriginMatcher
{
    private readonly List<EntradaExata> _exatas = [];
    private readonly List<EntradaCuringa> _curingas = [];
    private readonly List<string> _invalidas = [];
    private readonly bool _permitirLocalhost;

    public CorsOriginMatcher(IEnumerable<string>? origensConfiguradas, bool permitirLocalhost)
    {
        _permitirLocalhost = permitirLocalhost;

        foreach (var bruta in origensConfiguradas ?? [])
        {
            if (string.IsNullOrWhiteSpace(bruta))
                continue;

            var entrada = bruta.Trim().TrimEnd('/');

            if (TentarInterpretar(entrada, out var exata, out var curinga))
            {
                if (curinga is not null)
                    _curingas.Add(curinga);
                else if (exata is not null)
                    _exatas.Add(exata);
            }
            else
            {
                // Guardadas, e nao descartadas em silencio: o boot loga a lista e um erro de
                // digitacao em variavel de ambiente aparece na hora, nao no primeiro cliente.
                _invalidas.Add(entrada);
            }
        }
    }

    /// <summary>Entradas que nao puderam ser interpretadas. Logadas no boot.</summary>
    public IReadOnlyList<string> EntradasInvalidas => _invalidas;

    public IReadOnlyList<string> OrigensExatas => [.. _exatas.Select(e => e.Original)];

    public IReadOnlyList<string> OrigensCuringa => [.. _curingas.Select(e => e.Original)];

    /// <summary>Alguma origem valida foi configurada? Falso significa CORS totalmente fechado.</summary>
    public bool TemAlgumaOrigem => _exatas.Count > 0 || _curingas.Count > 0 || _permitirLocalhost;

    /// <summary>
    /// O predicado que vai no SetIsOriginAllowed.
    /// </summary>
    public bool Corresponde(string? origem)
    {
        if (string.IsNullOrWhiteSpace(origem))
            return false;

        if (!Uri.TryCreate(origem.Trim(), UriKind.Absolute, out var uri))
            return false;

        // Origem de navegador e sempre scheme://host[:porta], sem caminho, sem query, sem
        // userinfo. Recusar o resto fecha as variacoes exoticas usadas para enganar matcher.
        if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        if (!EsquemaSuportado(uri.Scheme))
            return false;

        // IsLoopback cobre localhost, 127.0.0.1 e [::1] e e imune a "localhost.atacante.com",
        // que um StartsWith("localhost") deixaria passar.
        if (_permitirLocalhost && uri.IsLoopback)
            return true;

        var host = uri.Host;
        var porta = uri.Port;
        var esquema = uri.Scheme;

        foreach (var exata in _exatas)
        {
            if (exata.Porta == porta
                && string.Equals(exata.Esquema, esquema, StringComparison.OrdinalIgnoreCase)
                && string.Equals(exata.Host, host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var curinga in _curingas)
        {
            if (curinga.Porta != porta
                || !string.Equals(curinga.Esquema, esquema, StringComparison.OrdinalIgnoreCase))
                continue;

            // O ponto separador e obrigatorio e o host tem de ser ESTRITAMENTE maior que a base,
            // senao "glorific.art" casaria com "*.glorific.art" e "evilglorific.art" tambem.
            var sufixo = "." + curinga.HostBase;

            if (host.Length > sufixo.Length && host.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool EsquemaSuportado(string esquema) =>
        string.Equals(esquema, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(esquema, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Interpreta a entrada configurada. O curinga e tratado por string ANTES do Uri: "*" nao e
    /// caractere valido de host e o parser do Uri rejeitaria a entrada inteira.
    /// </summary>
    private static bool TentarInterpretar(string entrada, out EntradaExata? exata, out EntradaCuringa? curinga)
    {
        exata = null;
        curinga = null;

        var separador = entrada.IndexOf("://", StringComparison.Ordinal);
        if (separador <= 0)
            return false;

        var esquema = entrada[..separador];
        var resto = entrada[(separador + 3)..];

        if (!EsquemaSuportado(esquema))
            return false;

        if (resto.Length == 0 || resto.Contains('/'))
            return false;

        if (!resto.StartsWith("*.", StringComparison.Ordinal))
        {
            // Sem curinga: deixa o proprio Uri validar e normalizar host e porta.
            if (resto.Contains('*') || !Uri.TryCreate(entrada, UriKind.Absolute, out var uri))
                return false;

            if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo))
                return false;

            exata = new EntradaExata(entrada, uri.Scheme, uri.Host, uri.Port);
            return true;
        }

        var hostBaseComPorta = resto[2..];

        // Curinga so no PRIMEIRO rotulo: o que sobra nao pode ter outro "*".
        if (hostBaseComPorta.Contains('*'))
            return false;

        // Reconstroi sem o curinga para o Uri validar host e resolver a porta default do esquema.
        if (!Uri.TryCreate($"{esquema}://{hostBaseComPorta}", UriKind.Absolute, out var uriBase))
            return false;

        if (uriBase.AbsolutePath != "/" || !string.IsNullOrEmpty(uriBase.UserInfo))
            return false;

        var hostBase = uriBase.Host;

        // Host base precisa ter ponto: "*.art" ou "*.com" liberaria um sufixo publico inteiro.
        if (!hostBase.Contains('.') || hostBase.StartsWith('.') || hostBase.EndsWith('.'))
            return false;

        curinga = new EntradaCuringa(entrada, uriBase.Scheme, hostBase, uriBase.Port);
        return true;
    }

    private sealed record EntradaExata(string Original, string Esquema, string Host, int Porta);

    private sealed record EntradaCuringa(string Original, string Esquema, string HostBase, int Porta);
}
