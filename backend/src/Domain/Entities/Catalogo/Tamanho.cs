using Glorific.Domain.Common;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// Tamanho como entidade, nunca string. Sem a coluna Ordem, "GG" ordena antes de "P"
/// alfabeticamente e o seletor de tamanho da pagina de produto sai errado.
/// </summary>
public class Tamanho : BaseEntity
{
    public required string Codigo { get; set; }
    public string? Descricao { get; set; }
    public int Ordem { get; set; }
    public GradeTamanho Grade { get; set; } = GradeTamanho.Alfa;
    public bool Ativo { get; set; } = true;

    public ICollection<ProdutoVariacao> Variacoes { get; set; } = [];
}
