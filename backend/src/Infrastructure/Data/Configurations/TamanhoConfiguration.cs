using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class TamanhoConfiguration : IEntityTypeConfiguration<Tamanho>
{
    public void Configure(EntityTypeBuilder<Tamanho> builder)
    {
        builder.ToTable("tamanhos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(120);

        // Sem Ordem explicita "GG" vem antes de "P" e o seletor da PDP sai errado.
        builder.Property(x => x.Ordem).HasColumnName("ordem");

        builder.Property(x => x.Grade).HasColumnName("grade").HasConversion<int>();
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        // "38" existe na grade numerica e pode existir na infantil: a unicidade e por grade.
        builder.HasIndex(x => new { x.Grade, x.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_tamanhos_grade_codigo");
    }
}
