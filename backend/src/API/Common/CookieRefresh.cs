using Glorific.Application.Ports.Options;

namespace Glorific.Api.Common;

/// <summary>
/// Escrita e leitura do cookie que carrega o refresh token.
///
/// A decisao e o motivo dela: o access token vive EM MEMORIA no front (15 minutos) e o refresh
/// vive num cookie httpOnly. Um XSS na loja consegue roubar o que esta em memoria e ficar com 15
/// minutos de acesso; nao consegue ler o cookie httpOnly e, portanto, nao consegue transformar o
/// ataque numa sessao de 30 dias renovavel. Guardar o refresh em localStorage inverteria isso.
///
/// Os quatro atributos, e o que cada um paga:
/// - HttpOnly: JavaScript nao le. E a razao inteira de o cookie existir.
/// - Secure: nao trafega em HTTP puro. Navegador moderno trata http://localhost como contexto
///   seguro, entao o desenvolvimento local continua funcionando.
/// - SameSite=Strict: o cookie nao sai em navegacao vinda de outro site. Loja e API ficam sob o
///   mesmo dominio registravel (glorific.art / api.glorific.art), entao o fluxo normal e
///   same-site. Custo real: um link externo que caia direto numa rota autenticada abre
///   deslogado, e o front resolve com o refresh silencioso na montagem do app.
/// - Path restrito a /api/v1/auth: o cookie nao e enviado em NENHUMA outra chamada. Menos
///   superficie e menos bytes em toda requisicao de catalogo.
/// </summary>
public static class CookieRefresh
{
    public static string? Ler(HttpRequest requisicao, JwtOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        ArgumentNullException.ThrowIfNull(opcoes);

        return requisicao.Cookies.TryGetValue(opcoes.RefreshCookieNome, out var valor) ? valor : null;
    }

    public static void Definir(HttpResponse resposta, JwtOptions opcoes, string token, DateTime expiraEmUtc)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentNullException.ThrowIfNull(opcoes);

        resposta.Cookies.Append(opcoes.RefreshCookieNome, token, Opcoes(opcoes, expiraEmUtc));
    }

    /// <summary>
    /// Apaga o cookie. Os atributos precisam ser IDENTICOS aos da escrita — Path diferente
    /// remove um cookie que nao existe e deixa o original vivo no navegador, e o usuario
    /// "deslogado" volta logado no proximo refresh silencioso.
    /// </summary>
    public static void Limpar(HttpResponse resposta, JwtOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentNullException.ThrowIfNull(opcoes);

        resposta.Cookies.Delete(
            opcoes.RefreshCookieNome,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = opcoes.RefreshCookiePath
            });
    }

    private static CookieOptions Opcoes(JwtOptions opcoes, DateTime expiraEmUtc) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = opcoes.RefreshCookiePath,

            // Expira junto com a linha em refresh_tokens: cookie que sobrevive ao registro so
            // gera 401 inexplicavel do lado do cliente.
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiraEmUtc, DateTimeKind.Utc)),

            IsEssential = true
        };
}
