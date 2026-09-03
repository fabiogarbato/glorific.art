using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

public class Midia : BaseEntity
{
    public required string Url { get; set; }

    /// <summary>Identificador no provedor de storage, necessario para deletar/transformar.</summary>
    public string? PublicId { get; set; }

    public string? AltText { get; set; }
    public int? Largura { get; set; }
    public int? Altura { get; set; }
    public long? TamanhoBytes { get; set; }
    public string? ContentType { get; set; }
    public DateTime DataCriacao { get; set; }
}
