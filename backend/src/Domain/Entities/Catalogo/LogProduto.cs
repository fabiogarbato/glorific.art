using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>Auditoria de ativacao/desativacao de produto — responde "quem tirou isso do ar".</summary>
public class LogProduto : BaseEntity
{
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public bool? AtivoAntigo { get; set; }
    public bool AtivoNovo { get; set; }

    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime DataAlteracao { get; set; }
}
