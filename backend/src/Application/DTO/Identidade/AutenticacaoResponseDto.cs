namespace Glorific.Application.DTO.Identidade;

/// <summary>
/// Corpo de /auth/login, /auth/register, /auth/google e /auth/refresh.
///
/// O refresh token NAO esta aqui de proposito: ele sai em cookie httpOnly, que o JavaScript da
/// pagina nao consegue ler. Devolver o refresh no corpo obrigaria o front a guarda-lo em algum
/// lugar acessivel por script, e ai um unico XSS troca uma sessao de 15 minutos por uma de 30
/// dias renovavel.
/// </summary>
public sealed record AutenticacaoResponseDto : ResponseDto
{
    /// <summary>JWT HS256. O front guarda EM MEMORIA, nunca em localStorage.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Segundos ate expirar. O front agenda a renovacao com isto.</summary>
    public required int ExpiresIn { get; init; }

    /// <summary>Sempre "Bearer": evita o front montar o header por concatenacao adivinhada.</summary>
    public string TokenType { get; init; } = "Bearer";

    public required UsuarioResponseDto Usuario { get; init; }
}
