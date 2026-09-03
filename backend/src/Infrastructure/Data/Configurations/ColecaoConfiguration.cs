using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ColecaoConfiguration : IEntityTypeConfiguration<Colecao>
{
    public void Configure(EntityTypeBuilder<Colecao> builder)
    {
        builder.ToTable("colecoes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(140).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasColumnType("text");
        builder.Property(x => x.Epigrafe).HasColumnName("epigrafe").HasColumnType("text");
        builder.Property(x => x.IdMidiaCapa).HasColumnName("id_midia_capa");
        builder.Property(x => x.IdMidiaBanner).HasColumnName("id_midia_banner");

        builder.Property(x => x.DataInicio)
            .HasColumnName("data_inicio").HasColumnType("timestamp without time zone");
        builder.Property(x => x.DataFim)
            .HasColumnName("data_fim").HasColumnType("timestamp without time zone");

        builder.Property(x => x.Destaque).HasColumnName("destaque").HasDefaultValue(false);
        builder.Property(x => x.Habilitado).HasColumnName("habilitado").HasDefaultValue(true);
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_colecoes_slug");

        builder.HasOne(x => x.MidiaCapa)
            .WithMany()
            .HasForeignKey(x => x.IdMidiaCapa)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MidiaBanner)
            .WithMany()
            .HasForeignKey(x => x.IdMidiaBanner)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
