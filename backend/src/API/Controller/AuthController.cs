using Glorific.Api.Common;
using Glorific.Api.Configuration;
using Glorific.Application.DTO.Identidade;
using Glorific.Application.Models.Auth;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Glorific.Api.Controller;

/// <summary>
/// Entrada e saida de sessao.
///
/// [Authorize] na classe e [AllowAnonymous] EXPLICITO em cada rota publica: com a FallbackPolicy
/// do projeto, esquecer o atributo vira 401 — falha barulhenta — em vez de endpoint aberto por
/// omissao. Aqui, deixar um endpoint publico e uma decisao visivel na revisao de codigo.
///
/// Rate limit em toda a classe: login, cadastro, refresh e recuperacao de senha sao o alvo
/// classico de forca bruta, e a policy roda ANTES da autenticacao, entao a tentativa e barrada
/// sem gastar validacao de token nem hash de senha.
///
/// O refresh token NUNCA aparece em corpo de resposta nesta classe. Ele entra e sai pelo cookie
/// httpOnly, e so.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting(PoliticasRateLimit.Auth)]
public sealed class AuthController : ControllerBase
{
    private readonly IAutenticacaoService _autenticacao;
    private readonly IUsuarioService _usuarios;
    private readonly JwtOptions _jwt;

    public AuthController(
        IAutenticacaoService autenticacao,
        IUsuarioService usuarios,
        IOptions<JwtOptions> jwt)
    {
        _autenticacao = autenticacao;
        _usuarios = usuarios;
        _jwt = jwt.Value;
    }

    /// <summary>Cadastro por e-mail e senha. Papel sempre cliente.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutenticacaoResponseDto>> Registrar(
        [FromBody] RegistroRequestDto dto,
        CancellationToken cancellationToken)
    {
        var sessao = await _autenticacao.RegistrarAsync(dto, Origem(), cancellationToken);
        return Responder(sessao);
    }

    /// <summary>Login por e-mail e senha.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AutenticacaoResponseDto>> Login(
        [FromBody] LoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        var sessao = await _autenticacao.LoginAsync(dto, Origem(), cancellationToken);
        return Responder(sessao);
    }

    /// <summary>
    /// Login com Google. O front obtem o id_token pelo GSI e manda aqui; quem valida a
    /// assinatura contra o JWKS do Google e o servidor.
    ///
    /// 401 = id_token invalido, expirado ou emitido para outro aplicativo.
    /// 400 = o Google assinou, mas a conta nao serve (e-mail nao verificado, conta desativada).
    /// 503 = esta loja nao configurou Google:ClientId. E problema NOSSO, nao do cliente, e a
    ///       mensagem diz isso em vez de acusar a credencial dele.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AutenticacaoResponseDto>> LoginGoogle(
        [FromBody] GoogleLoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        var sessao = await _autenticacao.LoginGoogleAsync(dto, Origem(), cancellationToken);
        return Responder(sessao);
    }

    /// <summary>
    /// Rotaciona o refresh token do cookie e devolve um access token novo.
    ///
    /// AllowAnonymous de proposito: quem chama esta rota esta justamente COM o access token
    /// expirado. A credencial aqui e o cookie, nao o header.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AutenticacaoResponseDto>> Renovar(CancellationToken cancellationToken)
    {
        var tokenAtual = CookieRefresh.Ler(Request, _jwt);

        try
        {
            var sessao = await _autenticacao.RenovarAsync(tokenAtual, Origem(), cancellationToken);
            return Responder(sessao);
        }
        catch (UnauthorizedAccessException)
        {
            // Limpar o cookie na falha evita o pior laco possivel: o navegador reenviando um
            // token morto a cada tentativa e o front achando que ainda ha sessao para renovar.
            CookieRefresh.Limpar(Response, _jwt);
            throw;
        }
    }

    /// <summary>Encerra a sessao atual (revoga a familia do refresh) e limpa o cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // AllowAnonymous e idempotente: sair tem de funcionar mesmo com o access token ja
        // expirado. Exigir autenticacao aqui faria o usuario "nao conseguir sair", e o cookie
        // de refresh continuaria valido no navegador.
        await _autenticacao.LogoutAsync(CookieRefresh.Ler(Request, _jwt), cancellationToken);

        CookieRefresh.Limpar(Response, _jwt);

        return NoContent();
    }

    /// <summary>Encerra TODAS as sessoes do usuario, em todos os dispositivos.</summary>
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutTodos(CancellationToken cancellationToken)
    {
        await _autenticacao.LogoutTodosAsync(User.ObterUuid(), cancellationToken);

        CookieRefresh.Limpar(Response, _jwt);

        return NoContent();
    }

    /// <summary>
    /// Dispara o e-mail de redefinicao. Responde 204 SEMPRE, exista a conta ou nao: qualquer
    /// diferenca transformaria este endpoint num verificador de quais e-mails compram aqui.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EsqueciSenha(
        [FromBody] EsqueciSenhaRequestDto dto,
        CancellationToken cancellationToken)
    {
        await _autenticacao.EsqueciSenhaAsync(dto, cancellationToken);
        return NoContent();
    }

    /// <summary>Redefine a senha pelo token do e-mail e derruba todas as sessoes.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RedefinirSenha(
        [FromBody] RedefinirSenhaRequestDto dto,
        CancellationToken cancellationToken)
    {
        await _autenticacao.RedefinirSenhaAsync(dto, cancellationToken);

        // Quem redefiniu pode estar com um cookie de sessao antiga, ja revogada no banco.
        CookieRefresh.Limpar(Response, _jwt);

        return NoContent();
    }

    /// <summary>
    /// Troca a senha com o usuario logado. Devolve uma sessao NOVA porque todas as anteriores,
    /// inclusive a que fez a chamada, acabam de ser revogadas.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AutenticacaoResponseDto>> TrocarSenha(
        [FromBody] TrocarSenhaRequestDto dto,
        CancellationToken cancellationToken)
    {
        var sessao = await _autenticacao.TrocarSenhaAsync(User.ObterUuid(), dto, Origem(), cancellationToken);
        return Responder(sessao);
    }

    /// <summary>Perfil, papeis e flags (temSenha, googleVinculado) do usuario logado.</summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioResponseDto>> Eu(CancellationToken cancellationToken)
    {
        var perfil = await _usuarios.ObterPerfilAsync(User.ObterUuid(), cancellationToken);
        return Ok(perfil);
    }

    /// <summary>Vincula uma conta Google a um usuario que ja existe e esta autenticado.</summary>
    [HttpPost("link-google")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UsuarioResponseDto>> VincularGoogle(
        [FromBody] GoogleLoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        var perfil = await _autenticacao.VincularGoogleAsync(User.ObterUuid(), dto, cancellationToken);
        return Ok(perfil);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Ponto UNICO em que a sessao vira resposta HTTP: o refresh vai para o cookie, o resto vai
    /// para o corpo. Centralizado para que nenhum endpoint novo esqueca o cookie — ou, pior,
    /// devolva o refresh token no JSON.
    /// </summary>
    private ActionResult<AutenticacaoResponseDto> Responder(SessaoAutenticada sessao)
    {
        CookieRefresh.Definir(Response, _jwt, sessao.RefreshTokenClaro, sessao.RefreshTokenExpiraEmUtc);

        return Ok(new AutenticacaoResponseDto
        {
            AccessToken = sessao.AccessToken,
            ExpiresIn = sessao.ExpiraEmSegundos,
            Usuario = sessao.Usuario
        });
    }

    /// <summary>
    /// IP e User-Agent para a auditoria da linha de refresh_tokens. O IP so e confiavel depois
    /// do UseForwardedHeaders, que ja rodou no pipeline antes de chegar aqui.
    /// </summary>
    private OrigemRequisicao Origem()
    {
        var userAgent = Request.Headers.UserAgent.ToString();

        return new OrigemRequisicao
        {
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent
        };
    }
}
