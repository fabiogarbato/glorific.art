using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class LogProdutoConfiguration : IEntityTypeConfiguration<LogProduto>
{
    public void Configure(EntityTypeBuilder<LogProduto> builder)
    {
        builder.ToTable("logs_produtos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");
        builder.Property(x => x.AtivoAntigo).HasColumnName("ativo_antigo");
        builder.Property(x => x.AtivoNovo).HasColumnName("ativo_novo");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");

        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone").IsRequired();

        // Auditoria sobrevive a desativacao do produto: Restrict nos dois lados.
        builder.HasOne(x => x.Produto)
            .WithMany(x => x.Logs)
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
