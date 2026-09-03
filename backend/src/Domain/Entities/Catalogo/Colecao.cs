using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// Curadoria temporal ("Capsula Advento", "Linha Salmos"). E ortogonal a categoria:
/// um vestido esta na categoria "Vestidos" E na colecao "Advento".
/// DataInicio/DataFim permitem agendar o drop.
/// </summary>
public class Colecao : BaseEntity, IAuditable
{
    public required string Nome { get; set; }
    public required string Slug { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Versiculo ou frase que abre a colecao na vitrine.</summary>
    public string? Epigrafe { get; set; }

    public int? IdMidiaCapa { get; set; }
    public Midia? MidiaCapa { get; set; }

    public int? IdMidiaBanner { get; set; }
    public Midia? MidiaBanner { get; set; }

    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }

    public bool Destaque { get; set; }
    public bool Habilitado { get; set; } = true;
    public int Ordem { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<ProdutoColecao> Produtos { get; set; } = [];
}
