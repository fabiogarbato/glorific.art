using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ProdutoVariacaoConfiguration : IEntityTypeConfiguration<ProdutoVariacao>
{
    public void Configure(EntityTypeBuilder<ProdutoVariacao> builder)
    {
        // Peso e dimensao positivos sao rede de seguranca do banco para a armadilha nº 3:
        // sem eles POST /api/shipment/calculate do Melhor Envio devolve 422 ou cota errado.
        builder.ToTable("produto_variacoes", t => t.HasCheckConstraint(
            "ck_produto_variacoes_dimensoes",
            "peso_gramas > 0 AND altura_cm > 0 AND largura_cm > 0 AND comprimento_cm > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(60).IsRequired();
        builder.Property(x => x.IdTamanho).HasColumnName("id_tamanho");
        builder.Property(x => x.IdCor).HasColumnName("id_cor");
        builder.Property(x => x.PrecoCentavos).HasColumnName("preco_centavos");
        builder.Property(x => x.CodigoBarras).HasColumnName("codigo_barras").HasMaxLength(20);

        builder.Property(x => x.PesoGramas).HasColumnName("peso_gramas");
        builder.Property(x => x.AlturaCm).HasColumnName("altura_cm").HasPrecision(8, 2);
        builder.Property(x => x.LarguraCm).HasColumnName("largura_cm").HasPrecision(8, 2);
        builder.Property(x => x.ComprimentoCm).HasColumnName("comprimento_cm").HasPrecision(8, 2);

        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        // Propriedade calculada de leitura: mora no Domain, nao no banco.
        builder.Ignore(x => x.PrecoEfetivoCentavos);

        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("ux_produto_variacoes_sku");

        // A combinacao tamanho + cor e o SKU logico: duplicar isso e oversell garantido.
        builder.HasIndex(x => new { x.IdProduto, x.IdTamanho, x.IdCor })
            .IsUnique()
            .HasDatabaseName("ux_produto_variacoes_produto_tamanho_cor");

        builder.HasQueryFilter(x => x.Ativo);

        builder.HasOne(x => x.Produto)
            .WithMany(x => x.Variacoes)
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tamanho)
            .WithMany(x => x.Variacoes)
            .HasForeignKey(x => x.IdTamanho)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cor)
            .WithMany(x => x.Variacoes)
            .HasForeignKey(x => x.IdCor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
