using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

public class ProdutoColecao : BaseEntity
{
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public int IdColecao { get; set; }
    public Colecao Colecao { get; set; } = null!;

    /// <summary>Ordem de exibicao dentro da colecao — a curadoria da vitrine e manual.</summary>
    public int Ordem { get; set; }
}
