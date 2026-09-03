using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class TabelaMedidasConfiguration : IEntityTypeConfiguration<TabelaMedidas>
{
    public void Configure(EntityTypeBuilder<TabelaMedidas> builder)
    {
        builder.ToTable("tabelas_medidas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Observacao).HasColumnName("observacao").HasColumnType("text");
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
    }
}
