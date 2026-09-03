using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "Jwt". POCO puro: sem IOptions, sem IConfiguration, sem nada de ASP.NET — o bind e o
/// ValidateDataAnnotations acontecem na API, esta classe so descreve a forma.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Segredo HS256. Minimo de 32 caracteres porque chave curta em HMAC-SHA256 e quebravel.
    ///
    /// NUNCA usar <see cref="Key"/> direto: use <see cref="KeyEfetiva"/>. No repo de referencia a
    /// chave era validada com Trim() num lugar e lida sem Trim() em outro; uma env var com
    /// espaco no fim passava na validacao do boot e invalidava TODA assinatura em runtime.
    /// </summary>
    [Required(ErrorMessage = "Jwt:Key e obrigatoria.")]
    [MinLength(32, ErrorMessage = "Jwt:Key precisa ter ao menos 32 caracteres.")]
    public string Key { get; set; } = string.Empty;

    /// <summary>A chave como ela deve ser usada em TODO lugar: emissao, validacao e boot.</summary>
    public string KeyEfetiva => (Key ?? string.Empty).Trim();

    /// <summary>Ex.: https://api.glorific.art</summary>
    [Required(ErrorMessage = "Jwt:Issuer e obrigatorio.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Ex.: https://glorific.art</summary>
    [Required(ErrorMessage = "Jwt:Audience e obrigatoria.")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Vida do access token. Curta de proposito: quem renova e o refresh.</summary>
    [Range(1, 1440, ErrorMessage = "Jwt:AccessTokenMinutos deve estar entre 1 e 1440.")]
    public int AccessTokenMinutos { get; set; } = 15;

    /// <summary>Vida do refresh token e do cookie httpOnly.</summary>
    [Range(1, 365, ErrorMessage = "Jwt:RefreshTokenDias deve estar entre 1 e 365.")]
    public int RefreshTokenDias { get; set; } = 30;

    /// <summary>
    /// Tolerancia de relogio na validacao. O default do framework e 5 minutos, o que mascara
    /// expiracao e faz token morto continuar aceito por tempo demais.
    /// </summary>
    [Range(0, 300, ErrorMessage = "Jwt:ClockSkewSegundos deve estar entre 0 e 300.")]
    public int ClockSkewSegundos { get; set; } = 30;

    /// <summary>Nome do cookie que carrega o refresh token opaco.</summary>
    [Required]
    public string RefreshCookieNome { get; set; } = "gl_rt";

    /// <summary>Path do cookie: restringe o envio ao endpoint de auth, reduzindo a superficie.</summary>
    [Required]
    public string RefreshCookiePath { get; set; } = "/api/v1/auth";
}
