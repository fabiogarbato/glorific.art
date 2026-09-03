using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SkuBase).HasColumnName("sku_base").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasColumnType("text");
        builder.Property(x => x.IdCategoria).HasColumnName("id_categoria");

        // Enum do Domain gravado como int — sem tabela lookup.
        builder.Property(x => x.Genero).HasColumnName("genero").HasConversion<int>();

        // Dinheiro sempre em centavos, coluna integer. Nunca decimal, nunca float.
        builder.Property(x => x.PrecoBaseCentavos).HasColumnName("preco_base_centavos");
        builder.Property(x => x.PrecoComparativoCentavos).HasColumnName("preco_comparativo_centavos");

        builder.Property(x => x.ComposicaoTecido).HasColumnName("composicao_tecido").HasMaxLength(400);
        builder.Property(x => x.InstrucoesLavagem).HasColumnName("instrucoes_lavagem").HasColumnType("text");
        builder.Property(x => x.Modelagem).HasColumnName("modelagem").HasConversion<int?>();
        builder.Property(x => x.IdTabelaMedidas).HasColumnName("id_tabela_medidas");
        builder.Property(x => x.Destaque).HasColumnName("destaque").HasDefaultValue(false);
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);
        builder.Property(x => x.MetaTitle).HasColumnName("meta_title").HasMaxLength(200);
        builder.Property(x => x.MetaDescription).HasColumnName("meta_description").HasMaxLength(400);

        // Denormalizado: a listagem exibe estrelas em 40 cards por pagina.
        builder.Property(x => x.NotaMedia).HasColumnName("nota_media").HasPrecision(2, 1);
        builder.Property(x => x.TotalAvaliacoes).HasColumnName("total_avaliacoes").HasDefaultValue(0);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_produtos_slug");
        builder.HasIndex(x => x.SkuBase).IsUnique().HasDatabaseName("ux_produtos_sku_base");

        // Listagem de catalogo filtra sempre por categoria + ativo.
        builder.HasIndex(x => new { x.IdCategoria, x.Ativo }).HasDatabaseName("ix_produtos_categoria_ativo");

        // Soft delete: o produto nunca e apagado porque o historico de pedidos depende dele.
        // Escape hatch obrigatorio no historico de pedido: IgnoreQueryFilters().
        builder.HasQueryFilter(x => x.Ativo);

        builder.HasOne(x => x.Categoria)
            .WithMany(x => x.Produtos)
            .HasForeignKey(x => x.IdCategoria)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TabelaMedidas)
            .WithMany(x => x.Produtos)
            .HasForeignKey(x => x.IdTabelaMedidas)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
