using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Identidade;

/// <summary>
/// Vinculo com provedor externo. A identidade do Google e o SubjectId (o claim sub), NAO o e-mail:
/// o e-mail de uma conta Google pode mudar, o sub e imutavel — casar por e-mail deixa a conta
/// orfa no dia em que o cliente troca o endereco.
///
/// Tabela separada permite Apple Sign-In depois sem migracao e permite o mesmo usuario ter
/// senha E Google ao mesmo tempo.
/// </summary>
public class LoginExterno : BaseEntity
{
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Minusculo: google, apple.</summary>
    public required string Provedor { get; set; }

    public required string SubjectId { get; set; }

    /// <summary>Guardado so para auditoria — nunca e usado para casar a conta.</summary>
    public required string EmailNoProvedor { get; set; }

    public DateTime DataVinculo { get; set; }
    public DateTime? UltimoUsoEm { get; set; }
}
