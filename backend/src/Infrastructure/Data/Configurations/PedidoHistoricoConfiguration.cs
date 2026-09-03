using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class PedidoHistoricoConfiguration : IEntityTypeConfiguration<PedidoHistorico>
{
    public void Configure(EntityTypeBuilder<PedidoHistorico> builder)
    {
        builder.ToTable("pedido_historicos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");

        // Null na criacao do pedido, quando nao existe status anterior.
        builder.Property(x => x.StatusAnterior).HasColumnName("status_anterior").HasConversion<int?>();
        builder.Property(x => x.StatusNovo).HasColumnName("status_novo").HasConversion<int>();

        // Null significa sistema: worker ou webhook do gateway.
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(400);

        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone").IsRequired();

        // Trilha de auditoria: responde "quem cancelou este pedido" mesmo depois de tudo.
        builder.HasOne(x => x.Pedido)
            .WithMany(x => x.Historico)
            .HasForeignKey(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
