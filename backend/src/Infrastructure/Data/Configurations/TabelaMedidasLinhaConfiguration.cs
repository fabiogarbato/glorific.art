using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class TabelaMedidasLinhaConfiguration : IEntityTypeConfiguration<TabelaMedidasLinha>
{
    public void Configure(EntityTypeBuilder<TabelaMedidasLinha> builder)
    {
        builder.ToTable("tabelas_medidas_linhas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdTabelaMedidas).HasColumnName("id_tabela_medidas");
        builder.Property(x => x.IdTamanho).HasColumnName("id_tamanho");

        builder.Property(x => x.BustoCm).HasColumnName("busto_cm").HasPrecision(8, 2);
        builder.Property(x => x.CinturaCm).HasColumnName("cintura_cm").HasPrecision(8, 2);
        builder.Property(x => x.QuadrilCm).HasColumnName("quadril_cm").HasPrecision(8, 2);
        builder.Property(x => x.ComprimentoCm).HasColumnName("comprimento_cm").HasPrecision(8, 2);
        builder.Property(x => x.MangaCm).HasColumnName("manga_cm").HasPrecision(8, 2);
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);

        // Linha e filha do agregado tabela de medidas.
        builder.HasOne(x => x.TabelaMedidas)
            .WithMany(x => x.Linhas)
            .HasForeignKey(x => x.IdTabelaMedidas)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tamanho)
            .WithMany()
            .HasForeignKey(x => x.IdTamanho)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
