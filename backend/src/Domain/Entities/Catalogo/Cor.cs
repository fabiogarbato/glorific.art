using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// O swatch de cor e elemento obrigatorio de UI em moda. HexRgb cobre cor solida;
/// IdMidiaSwatch cobre estampa (xadrez, floral), onde uma cor chapada nao representa a peca.
/// </summary>
public class Cor : BaseEntity
{
    public required string Nome { get; set; }
    public required string Slug { get; set; }

    /// <summary>Formato #RRGGBB.</summary>
    public required string HexRgb { get; set; }

    public int? IdMidiaSwatch { get; set; }
    public Midia? MidiaSwatch { get; set; }

    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<ProdutoVariacao> Variacoes { get; set; } = [];
}
