using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class MidiaProdutoConfiguration : IEntityTypeConfiguration<MidiaProduto>
{
    public void Configure(EntityTypeBuilder<MidiaProduto> builder)
    {
        builder.ToTable("midias_produtos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");
        builder.Property(x => x.IdMidia).HasColumnName("id_midia");
        builder.Property(x => x.IdCor).HasColumnName("id_cor");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);
        builder.Property(x => x.EhCapa).HasColumnName("eh_capa").HasDefaultValue(false);

        builder.HasIndex(x => new { x.IdProduto, x.IdMidia })
            .IsUnique()
            .HasDatabaseName("ux_midias_produtos_produto_midia");

        // Filho de agregado: a galeria nao existe sem o produto.
        builder.HasOne(x => x.Produto)
            .WithMany(x => x.Midias)
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Cascade);

        // A midia e compartilhada (catalogo e avaliacao usam a mesma tabela): nunca cascade.
        builder.HasOne(x => x.Midia)
            .WithMany()
            .HasForeignKey(x => x.IdMidia)
            .OnDelete(DeleteBehavior.Restrict);

        // Galeria por cor: clicar no swatch troca as fotos.
        builder.HasOne(x => x.Cor)
            .WithMany()
            .HasForeignKey(x => x.IdCor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
