using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class MidiaConfiguration : IEntityTypeConfiguration<Midia>
{
    public void Configure(EntityTypeBuilder<Midia> builder)
    {
        builder.ToTable("midias");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Url).HasColumnName("url").HasColumnType("text").IsRequired();
        builder.Property(x => x.PublicId).HasColumnName("public_id").HasMaxLength(255);
        builder.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(255);
        builder.Property(x => x.Largura).HasColumnName("largura");
        builder.Property(x => x.Altura).HasColumnName("altura");
        builder.Property(x => x.TamanhoBytes).HasColumnName("tamanho_bytes");
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
    }
}
