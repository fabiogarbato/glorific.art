using Glorific.Application.DTO.Identidade;
using Glorific.Application.Models.Auth;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Casos de uso de autenticacao. Nao herda IGenericService: nenhum deles e CRUD sobre um
/// agregado — sao transicoes de estado de sessao, cada uma com a sua regra.
///
/// Todo metodo que abre sessao devolve <see cref="SessaoAutenticada"/>, e nunca o DTO de
/// resposta pronto: e o controller que decide o que vai no cookie e o que vai no corpo.
/// </summary>
public interface IAutenticacaoService
{
    /// <summary>
    /// Cadastro publico por e-mail e senha. O papel e SEMPRE cliente — nao existe caminho pelo
    /// qual o corpo da requisicao influencie isso.
    /// </summary>
    Task<SessaoAutenticada> RegistrarAsync(
        RegistroRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Login por e-mail e senha. Conta inexistente, sem senha (so Google), com senha errada ou
    /// desativada devolvem a MESMA falha: distinguir transforma o login num verificador de
    /// quais e-mails tem conta na loja.
    /// </summary>
    Task<SessaoAutenticada> LoginAsync(
        LoginRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Login com id_token do Google. Resolve a identidade por (provedor, sub); se nao achar,
    /// casa por e-mail JA VERIFICADO pelo Google e vincula; se ainda nao achar, cria a conta
    /// sem senha. Papel nunca vem do provedor.
    /// </summary>
    Task<SessaoAutenticada> LoginGoogleAsync(
        GoogleLoginRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotaciona o refresh token: revoga o atual e emite o proximo na MESMA familia.
    /// Reapresentar um token ja substituido e roubo — a familia inteira e revogada e a chamada
    /// falha com 401.
    /// </summary>
    Task<SessaoAutenticada> RenovarAsync(
        string? refreshTokenClaro,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default);

    /// <summary>Revoga a familia do token apresentado. Idempotente: token invalido nao e erro.</summary>
    Task LogoutAsync(string? refreshTokenClaro, CancellationToken cancellationToken = default);

    /// <summary>Revoga TODAS as sessoes do usuario, em todos os dispositivos.</summary>
    Task LogoutTodosAsync(string uuidUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca a senha exigindo a atual e derruba todas as sessoes existentes, devolvendo uma
    /// sessao NOVA para quem trocou. O repo de referencia nao invalidava nada na troca: quem
    /// trocava a senha por suspeita de invasao continuava com o invasor logado do outro lado.
    /// </summary>
    Task<SessaoAutenticada> TrocarSenhaAsync(
        string uuidUsuario,
        TrocarSenhaRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispara o e-mail com o link de redefinicao. NUNCA sinaliza se o e-mail existe — a
    /// resposta e a mesma nos dois casos.
    /// </summary>
    Task EsqueciSenhaAsync(EsqueciSenhaRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redefine a senha pelo token de uso unico e revoga todas as sessoes. Nao devolve sessao:
    /// depois de redefinir, o usuario entra de novo — se o link vazou, quem o usou nao sai
    /// logado do outro lado.
    /// </summary>
    Task RedefinirSenhaAsync(RedefinirSenhaRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Vincula uma conta Google a um usuario que ja existe e esta autenticado.</summary>
    Task<UsuarioResponseDto> VincularGoogleAsync(
        string uuidUsuario,
        GoogleLoginRequestDto dto,
        CancellationToken cancellationToken = default);
}
