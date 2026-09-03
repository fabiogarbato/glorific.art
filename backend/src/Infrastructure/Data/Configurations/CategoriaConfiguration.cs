using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(140).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasColumnType("text");
        builder.Property(x => x.IdCategoriaPai).HasColumnName("id_categoria_pai");
        builder.Property(x => x.IdMidiaCapa).HasColumnName("id_midia_capa");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);
        builder.Property(x => x.Habilitado).HasColumnName("habilitado").HasDefaultValue(true);
        builder.Property(x => x.MetaTitle).HasColumnName("meta_title").HasMaxLength(200);
        builder.Property(x => x.MetaDescription).HasColumnName("meta_description").HasMaxLength(400);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        // URL de catalogo e SEO-critica: /vestidos/midi nao pode existir duas vezes.
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_categorias_slug");

        // Auto-relacao de um nivel. Restrict: apagar "Vestidos" nao pode arrastar "Midi".
        builder.HasOne(x => x.CategoriaPai)
            .WithMany(x => x.Filhas)
            .HasForeignKey(x => x.IdCategoriaPai)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MidiaCapa)
            .WithMany()
            .HasForeignKey(x => x.IdMidiaCapa)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
