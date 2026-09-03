using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class PedidoItemConfiguration : IEntityTypeConfiguration<PedidoItem>
{
    public void Configure(EntityTypeBuilder<PedidoItem> builder)
    {
        builder.ToTable("pedido_itens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");

        // FKs so para relatorio de curva ABC: nenhuma tela de cliente depende delas.
        builder.Property(x => x.IdVariacao).HasColumnName("id_variacao");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");

        // Armadilha nº 2: a linha e imutavel e autossuficiente. Renomear um produto nao pode
        // reescrever o recibo de um pedido de dois anos atras.
        builder.Property(x => x.SkuSnapshot).HasColumnName("sku_snapshot").HasMaxLength(60).IsRequired();
        builder.Property(x => x.NomeProdutoSnapshot)
            .HasColumnName("nome_produto_snapshot").HasMaxLength(180).IsRequired();
        builder.Property(x => x.TamanhoSnapshot)
            .HasColumnName("tamanho_snapshot").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CorSnapshot).HasColumnName("cor_snapshot").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ImagemUrlSnapshot).HasColumnName("imagem_url_snapshot").HasColumnType("text");

        builder.Property(x => x.Quantidade).HasColumnName("quantidade");
        builder.Property(x => x.PrecoUnitarioCentavos).HasColumnName("preco_unitario_centavos");
        builder.Property(x => x.DescontoUnitarioCentavos).HasColumnName("desconto_unitario_centavos");
        builder.Property(x => x.PesoGramasSnapshot).HasColumnName("peso_gramas_snapshot");

        // Gravado, nao calculado: o que vale e o valor efetivamente cobrado pelo gateway.
        builder.Property(x => x.TotalLinhaCentavos).HasColumnName("total_linha_centavos");

        builder.HasOne(x => x.Pedido)
            .WithMany(x => x.Itens)
            .HasForeignKey(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variacao)
            .WithMany()
            .HasForeignKey(x => x.IdVariacao)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Produto)
            .WithMany()
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
