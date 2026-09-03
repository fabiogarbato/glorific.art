using Glorific.Domain.Entities.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class AvaliacaoMidiaConfiguration : IEntityTypeConfiguration<AvaliacaoMidia>
{
    public void Configure(EntityTypeBuilder<AvaliacaoMidia> builder)
    {
        builder.ToTable("avaliacoes_midias");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdAvaliacao).HasColumnName("id_avaliacao");
        builder.Property(x => x.IdMidia).HasColumnName("id_midia");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);

        // Filho de agregado: a foto do review nao existe sem o review.
        builder.HasOne(x => x.Avaliacao)
            .WithMany(x => x.Midias)
            .HasForeignKey(x => x.IdAvaliacao)
            .OnDelete(DeleteBehavior.Cascade);

        // A tabela de midias e compartilhada com o catalogo: nunca cascade.
        builder.HasOne(x => x.Midia)
            .WithMany()
            .HasForeignKey(x => x.IdMidia)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
