namespace Glorific.Application.Models.Auth;

/// <summary>
/// Access token JWT recem-emitido.
///
/// ExpiraEmUtc e calculado com IClock.UtcNow, nunca com DateTime.Now: no repo de referencia um
/// token de 8 h emitido num host UTC-3 valia 5 h.
/// </summary>
public sealed record AccessTokenGerado
{
    /// <summary>JWT compacto, ja assinado.</summary>
    public required string Token { get; init; }

    public required DateTime ExpiraEmUtc { get; init; }

    /// <summary>Segundos ate expirar. E o expiresIn devolvido ao front.</summary>
    public required int ExpiraEmSegundos { get; init; }

    /// <summary>Claim "sid" — id da familia de refresh, ou seja, da sessao.</summary>
    public Guid? IdSessao { get; init; }
}

/// <summary>
/// Par do refresh token opaco.
///
/// TokenClaro vai UMA unica vez para o cookie httpOnly e nunca e persistido. O banco guarda so o
/// TokenHash (SHA-256): dump de banco vazado nao vira sessao valida.
/// </summary>
public sealed record RefreshTokenGerado
{
    /// <summary>32 bytes de RandomNumberGenerator em base64url. Nao e JWT — nao precisa ser lido.</summary>
    public required string TokenClaro { get; init; }

    /// <summary>SHA-256 do TokenClaro. E o que vai em refresh_tokens.token_hash.</summary>
    public required string TokenHash { get; init; }
}
