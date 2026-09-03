namespace Glorific.Application.Models.Auth;

/// <summary>
/// Identidade extraida de um id_token do Google JA VALIDADO (assinatura contra o JWKS, iss, aud,
/// exp e nbf conferidos pelo adaptador).
///
/// A porta devolve este record ou null; nenhum tipo da Google.Apis.Auth atravessa a fronteira.
///
/// Regras que o servico aplica em cima disto:
/// - <see cref="EmailVerificado"/> false -> 400 "E-mail Google nao verificado". Vincular conta
///   por e-mail nao verificado permite tomar a conta de outra pessoa so criando um Google com o
///   mesmo endereco.
/// - <see cref="Subject"/> vazio -> 400. E a chave estavel em logins_externos, nao o e-mail
///   (o usuario pode trocar o e-mail da conta Google).
/// - Papel NUNCA vem daqui. Usuario novo nasce sempre "cliente".
/// </summary>
public sealed record GoogleIdentityInfo
{
    /// <summary>Claim "sub". Chave de logins_externos junto com provedor = "google".</summary>
    public required string Subject { get; init; }

    /// <summary>Normalizar para minusculas antes de procurar em usuarios.</summary>
    public required string Email { get; init; }

    public bool EmailVerificado { get; init; }

    /// <summary>Claim "name".</summary>
    public string? Nome { get; init; }

    /// <summary>Claim "picture".</summary>
    public string? FotoUrl { get; init; }
}
