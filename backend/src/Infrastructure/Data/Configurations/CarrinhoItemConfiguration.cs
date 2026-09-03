using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarrinhoItemEntity = Glorific.Domain.Entities.Carrinho.CarrinhoItem;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CarrinhoItemConfiguration : IEntityTypeConfiguration<CarrinhoItemEntity>
{
    public void Configure(EntityTypeBuilder<CarrinhoItemEntity> builder)
    {
        builder.ToTable("carrinho_itens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdCarrinho).HasColumnName("id_carrinho");

        // Aponta para a VARIACAO: quem tem preco, peso e estoque e o SKU.
        builder.Property(x => x.IdVariacao).HasColumnName("id_variacao");
        builder.Property(x => x.Quantidade).HasColumnName("quantidade");

        // Nao e o preco cobrado: serve para avisar "o preco deste item mudou" antes do checkout.
        builder.Property(x => x.PrecoUnitarioSnapshotCentavos)
            .HasColumnName("preco_unitario_snapshot_centavos");

        builder.Property(x => x.DataAdicao)
            .HasColumnName("data_adicao").HasColumnType("timestamp without time zone").IsRequired();

        // Somar quantidade em vez de criar linha nova e regra do service; o indice e a rede.
        builder.HasIndex(x => new { x.IdCarrinho, x.IdVariacao })
            .IsUnique()
            .HasDatabaseName("ux_carrinho_itens_carrinho_variacao");

        builder.HasOne(x => x.Carrinho)
            .WithMany(x => x.Itens)
            .HasForeignKey(x => x.IdCarrinho)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variacao)
            .WithMany()
            .HasForeignKey(x => x.IdVariacao)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
