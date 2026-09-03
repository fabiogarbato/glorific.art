using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Identidade;

/// <summary>
/// Refresh token rotativo com deteccao de reuso.
///
/// TokenHash guarda SHA-256 do token opaco, nunca o token em claro: vazamento de dump de banco
/// nao pode virar sessao valida.
/// IdFamilia amarra toda a cadeia de rotacoes de um mesmo login. Se um token ja substituido for
/// apresentado de novo, ele foi roubado — a resposta e revogar a familia inteira, nao so a linha.
/// </summary>
public class RefreshToken : BaseEntity
{
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTime ExpiraEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? RevogadoEm { get; set; }

    /// <summary>Hash do token que sucedeu este na rotacao. Preenchido = este ja foi usado.</summary>
    public string? SubstituidoPorHash { get; set; }

    public Guid IdFamilia { get; set; }

    public string? IpCriacao { get; set; }
    public string? UserAgent { get; set; }
}
