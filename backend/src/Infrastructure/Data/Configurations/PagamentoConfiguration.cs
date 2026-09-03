using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");
        builder.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Metodo).HasColumnName("metodo").HasMaxLength(40);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.ValorCentavos).HasColumnName("valor_centavos");
        builder.Property(x => x.Parcelas).HasColumnName("parcelas");

        // Dois identificadores porque o webhook chega ora com o id do pedido no gateway,
        // ora com o id da cobranca — procurar so por um deixa evento orfao.
        builder.Property(x => x.ProviderOrderId).HasColumnName("provider_order_id").HasMaxLength(120);
        builder.Property(x => x.ProviderChargeId).HasColumnName("provider_charge_id").HasMaxLength(120);

        builder.Property(x => x.PaymentUrl).HasColumnName("payment_url").HasColumnType("text");
        builder.Property(x => x.QrCodePix).HasColumnName("qr_code_pix").HasColumnType("text");
        builder.Property(x => x.LinhaDigitavel).HasColumnName("linha_digitavel").HasMaxLength(120);

        builder.Property(x => x.ExpiraEm)
            .HasColumnName("expira_em").HasColumnType("timestamp without time zone");

        // Payload cru em jsonb: quando o gateway muda contrato sem avisar, e a unica forma de
        // reconstruir o que aconteceu sem pedir log ao suporte deles.
        builder.Property(x => x.RawUltimaResposta)
            .HasColumnName("raw_ultima_resposta").HasColumnType("jsonb");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataConfirmacao)
            .HasColumnName("data_confirmacao").HasColumnType("timestamp without time zone");

        // Um pagamento por pedido, garantido no banco e nao por if no service.
        builder.HasIndex(x => x.IdPedido).IsUnique().HasDatabaseName("ux_pagamentos_pedido");
        builder.HasIndex(x => x.ProviderOrderId).HasDatabaseName("ix_pagamentos_provider_order_id");
        builder.HasIndex(x => x.ProviderChargeId).HasDatabaseName("ix_pagamentos_provider_charge_id");

        builder.HasOne(x => x.Pedido)
            .WithOne(x => x.Pagamento)
            .HasForeignKey<Pagamento>(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
