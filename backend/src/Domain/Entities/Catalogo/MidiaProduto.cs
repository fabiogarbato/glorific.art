using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// Galeria do produto. IdCor nullable permite galeria POR COR — clicar no swatch
/// "Terracota" troca as fotos, que e o comportamento esperado em moda.
/// Ordem e explicita: deduzir a capa por "menor Id" quebra a cada reupload.
/// </summary>
public class MidiaProduto : BaseEntity
{
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public int IdMidia { get; set; }
    public Midia Midia { get; set; } = null!;

    public int? IdCor { get; set; }
    public Cor? Cor { get; set; }

    public int Ordem { get; set; }
    public bool EhCapa { get; set; }
}
