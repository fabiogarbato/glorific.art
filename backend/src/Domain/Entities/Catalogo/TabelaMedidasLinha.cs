using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Catalogo;

public class TabelaMedidasLinha : BaseEntity
{
    public int IdTabelaMedidas { get; set; }
    public TabelaMedidas TabelaMedidas { get; set; } = null!;

    public int IdTamanho { get; set; }
    public Tamanho Tamanho { get; set; } = null!;

    public decimal? BustoCm { get; set; }
    public decimal? CinturaCm { get; set; }
    public decimal? QuadrilCm { get; set; }
    public decimal? ComprimentoCm { get; set; }
    public decimal? MangaCm { get; set; }
    public int Ordem { get; set; }
}
