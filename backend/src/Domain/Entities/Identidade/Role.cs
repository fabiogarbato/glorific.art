using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Identidade;

/// <summary>
/// Papel como linha de tabela, nunca string livre em coluna de usuario.
/// Modelar N:N desde o dia 1 evita a migracao dolorosa quando "operador de expedicao"
/// precisar existir separado de "gerente de catalogo".
/// </summary>
public class Role : BaseEntity
{
    /// <summary>Minusculo e sem espaco: e o valor da claim role no JWT.</summary>
    public required string Nome { get; set; }

    public string? Descricao { get; set; }

    public ICollection<UsuarioRole> Usuarios { get; set; } = [];
}
