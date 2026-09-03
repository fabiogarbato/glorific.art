using Glorific.Application.Models.Auth;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de validacao do id_token do Google (fluxo GSI: o front obtem o id_token, o back valida
/// e emite o NOSSO JWT).
///
/// O adaptador valida assinatura contra o JWKS do Google, iss, aud (nosso ClientId), exp e nbf
/// com tolerancia de relogio. Nenhum tipo da Google.Apis.Auth atravessa esta porta.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Valida o id_token.
    /// </summary>
    /// <returns>
    /// A identidade quando o token e valido; <c>null</c> quando e invalido, expirado, de outra
    /// audience ou malformado.
    ///
    /// Retorna null em vez de lancar porque token invalido e caso ESPERADO no endpoint de login
    /// (o servico traduz para 401), nao falha de infraestrutura. Falha de rede ao buscar o JWKS,
    /// essa sim, propaga como excecao — sao problemas diferentes e nao podem virar a mesma
    /// resposta.
    ///
    /// Atencao: retorno nao-nulo significa apenas "o Google assinou isto". As guardas de negocio
    /// (EmailVerificado, Subject nao vazio, usuario ativo) sao do servico.
    /// </returns>
    Task<GoogleIdentityInfo?> ValidarAsync(string idToken, CancellationToken ct = default);
}
