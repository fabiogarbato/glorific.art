using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CorConfiguration : IEntityTypeConfiguration<Cor>
{
    public void Configure(EntityTypeBuilder<Cor> builder)
    {
        builder.ToTable("cores");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();

        // Formato #RRGGBB — sete caracteres exatos.
        builder.Property(x => x.HexRgb).HasColumnName("hex_rgb").HasMaxLength(7).IsRequired();

        builder.Property(x => x.IdMidiaSwatch).HasColumnName("id_midia_swatch");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_cores_slug");

        builder.HasOne(x => x.MidiaSwatch)
            .WithMany()
            .HasForeignKey(x => x.IdMidiaSwatch)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
