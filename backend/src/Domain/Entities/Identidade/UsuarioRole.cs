namespace Glorific.Domain.Entities.Identidade;

/// <summary>
/// Vinculo usuario-papel. Nao herda BaseEntity porque a identidade da linha e o proprio par
/// (IdUsuario, IdRole): uma PK sintetica permitiria gravar o mesmo papel duas vezes.
/// ConcedidaPor responde "quem promoveu este usuario a admin", que e a pergunta de auditoria
/// mais cara de responder depois que o estrago aconteceu.
/// </summary>
public class UsuarioRole
{
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int IdRole { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime ConcedidaEm { get; set; }

    /// <summary>Null quando veio do seed ou do cadastro automatico de cliente.</summary>
    public int? ConcedidaPor { get; set; }
    public Usuario? UsuarioConcedente { get; set; }
}
