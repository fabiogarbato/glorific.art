using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// Taxonomia do catalogo, com auto-relacao de um nivel ("Vestidos" > "Midi").
/// O repo de referencia usava Categoria + SubCategoria + join N:N — tres tabelas
/// para a mesma expressividade.
/// </summary>
public class Categoria : BaseEntity, IAuditable
{
    public required string Nome { get; set; }
    public required string Slug { get; set; }
    public string? Descricao { get; set; }

    public int? IdCategoriaPai { get; set; }
    public Categoria? CategoriaPai { get; set; }

    public int? IdMidiaCapa { get; set; }
    public Midia? MidiaCapa { get; set; }

    public int Ordem { get; set; }
    public bool Habilitado { get; set; } = true;

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<Categoria> Filhas { get; set; } = [];
    public ICollection<Produto> Produtos { get; set; } = [];
}
