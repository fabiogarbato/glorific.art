using System.Security.Claims;

namespace Glorific.Api.Common;

/// <summary>
/// Leitura das claims do usuario logado.
///
/// Existe para que NENHUM controller escreva User.FindFirst("sub") a mao. No repo de referencia
/// o front lia decoded.nameidentifier — claim que nunca existiu no token — e user.uuid era
/// sempre undefined. O nome da claim precisa ser escrito em um lugar so, e este e o lugar.
///
/// Os nomes curtos ("sub", "role") sao os mesmos configurados em MapInboundClaims = false e em
/// NameClaimType/RoleClaimType. Mudar um sem o outro quebra a autorizacao inteira em silencio.
/// </summary>
public static class UsuarioAutenticado
{
    /// <summary>Identidade publica do usuario: usuarios.Uuid.</summary>
    public const string ClaimUuid = "sub";

    /// <summary>Familia do refresh token, ou seja, a sessao.</summary>
    public const string ClaimSessao = "sid";

    /// <summary>
    /// Uuid do usuario, ou 401 quando a claim nao esta la.
    ///
    /// Lanca em vez de devolver null de proposito: todo caminho que chama isto esta atras de
    /// [Authorize], entao "sem sub" significa token nosso emitido errado. Devolver string vazia
    /// faria a consulta seguinte procurar por "" e responder 404 — um bug bem mais dificil de
    /// enxergar do que um 401.
    /// </summary>
    public static string ObterUuid(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var uuid = usuario.FindFirstValue(ClaimUuid);

        if (string.IsNullOrWhiteSpace(uuid))
            throw new UnauthorizedAccessException("Token sem identificacao de usuario.");

        return uuid;
    }

    /// <summary>Sessao (familia de refresh) do token atual. Null quando o token nao carrega sid.</summary>
    public static Guid? ObterSessao(this ClaimsPrincipal usuario)
    {
        var valor = usuario?.FindFirstValue(ClaimSessao);

        return Guid.TryParse(valor, out var sessao) ? sessao : null;
    }
}
