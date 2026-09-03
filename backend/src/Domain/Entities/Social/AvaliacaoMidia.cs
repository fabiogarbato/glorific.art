using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Entities.Social;

/// <summary>
/// Foto enviada pelo cliente. Reaproveita a tabela de midias para o mesmo storage e a mesma
/// rotina de limpeza de orfas valerem para foto de catalogo e foto de review.
/// </summary>
public class AvaliacaoMidia : BaseEntity
{
    public int IdAvaliacao { get; set; }
    public Avaliacao Avaliacao { get; set; } = null!;

    public int IdMidia { get; set; }
    public Midia Midia { get; set; } = null!;

    public int Ordem { get; set; }
}
