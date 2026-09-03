using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

/// <summary>
/// "Guia de medidas" e o item numero 1 de reducao de devolucao em moda.
/// E compartilhada entre produtos da mesma modelagem, por isso tabela propria.
/// </summary>
public class TabelaMedidas : BaseEntity
{
    public required string Nome { get; set; }
    public string? Observacao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; }

    public ICollection<TabelaMedidasLinha> Linhas { get; set; } = [];
    public ICollection<Produto> Produtos { get; set; } = [];
}
