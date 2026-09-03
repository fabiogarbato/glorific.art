using Glorific.Application.Models.Auth;
using Glorific.Domain.Entities.Identidade;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de emissao de tokens. Fica em Ports, e nao em Services, porque a implementacao depende
/// de criptografia e de System.IdentityModel.Tokens.Jwt — pacotes que nao entram nesta camada.
///
/// Claims do access token (HS256): sub = usuarios.Uuid, email, name, role (uma por papel),
/// sid = id da familia de refresh, jti, iat, nbf, exp, iss, aud.
/// Regras: papel vem SEMPRE do banco (usuarios_roles), nunca de provedor externo; datas saem de
/// IClock.UtcNow; Jwt:Key e lida com Trim() no mesmo lugar em que e validada.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Emite o access token do usuario.
    /// </summary>
    /// <param name="usuario">Entidade ja carregada; Uuid, Email e NomeCompleto viram claims.</param>
    /// <param name="roles">
    /// Papeis vindos de usuarios_roles. Lista vazia gera token sem claim role — o que resulta em
    /// 403 nas policies administrativas, e esse e o comportamento correto.
    /// </param>
    /// <param name="idSessao">
    /// Id da familia de refresh, que vira a claim "sid". Null apenas em cenarios sem sessao
    /// persistida (token de servico, teste).
    /// </param>
    AccessTokenGerado GerarAccessToken(Usuario usuario, IEnumerable<string> roles, Guid? idSessao = null);

    /// <summary>
    /// Gera um refresh token opaco novo: 32 bytes de RandomNumberGenerator em base64url.
    ///
    /// Devolve o par (token em claro, hash). O claro vai UMA vez para o cookie httpOnly; o banco
    /// so recebe o hash. Nunca e JWT: nao precisa ser lido, so comparado.
    /// </summary>
    RefreshTokenGerado GerarRefreshToken();

    /// <summary>
    /// SHA-256 do token apresentado, na mesma codificacao usada por <see cref="GerarRefreshToken"/>.
    ///
    /// E o que permite localizar a linha na rotacao sem nunca guardar o segredo. A comparacao do
    /// resultado deve ser feita no banco (WHERE token_hash = @h), nunca com == sobre string em
    /// memoria depois de carregar a tabela.
    /// </summary>
    string HashRefreshToken(string token);
}
