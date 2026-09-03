using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Entities.Clientes;

/// <summary>
/// Lista de desejos no PRODUTO, com variacao opcional. Em moda o cliente favorita a peca
/// ("quero este vestido") antes de decidir o tamanho; exigir a variacao esvaziaria o recurso.
/// A variacao, quando informada, e o que permite o aviso de "voltou ao estoque" no tamanho certo.
/// </summary>
public class ListaDesejoItem : BaseEntity
{
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public int? IdVariacao { get; set; }
    public ProdutoVariacao? Variacao { get; set; }

    public DateTime DataCriacao { get; set; }
}
