using Glorific.Domain.Entities.Promocoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CupomUsoConfiguration : IEntityTypeConfiguration<CupomUso>
{
    public void Configure(EntityTypeBuilder<CupomUso> builder)
    {
        builder.ToTable("cupons_usos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdCupom).HasColumnName("id_cupom");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");

        // Gravado porque as regras (teto, restricao de categoria) podem mudar depois e o
        // relatorio de investimento em promocao precisa do numero real daquele dia.
        builder.Property(x => x.ValorDescontadoCentavos).HasColumnName("valor_descontado_centavos");

        builder.Property(x => x.DataUso)
            .HasColumnName("data_uso").HasColumnType("timestamp without time zone").IsRequired();

        // Impede o mesmo pedido consumir o cupom duas vezes numa retentativa de checkout.
        builder.HasIndex(x => new { x.IdCupom, x.IdPedido })
            .IsUnique()
            .HasDatabaseName("ux_cupons_usos_cupom_pedido");

        // Ledger: nada aqui e apagado em cascata.
        builder.HasOne(x => x.Cupom)
            .WithMany(x => x.Usos)
            .HasForeignKey(x => x.IdCupom)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pedido)
            .WithMany(x => x.CupomUsos)
            .HasForeignKey(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
