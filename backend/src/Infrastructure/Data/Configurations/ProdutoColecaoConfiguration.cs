using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ProdutoColecaoConfiguration : IEntityTypeConfiguration<ProdutoColecao>
{
    public void Configure(EntityTypeBuilder<ProdutoColecao> builder)
    {
        builder.ToTable("produtos_colecoes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");
        builder.Property(x => x.IdColecao).HasColumnName("id_colecao");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0);

        // O vinculo pertence ao produto: some com ele.
        builder.HasOne(x => x.Produto)
            .WithMany(x => x.Colecoes)
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Cascade);

        // A colecao e curadoria com vida propria: apagar drop nao apaga vinculo em silencio.
        builder.HasOne(x => x.Colecao)
            .WithMany(x => x.Produtos)
            .HasForeignKey(x => x.IdColecao)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
