using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class PagamentoEventoConfiguration : IEntityTypeConfiguration<PagamentoEvento>
{
    public void Configure(EntityTypeBuilder<PagamentoEvento> builder)
    {
        builder.ToTable("pagamentos_eventos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Nullable: o evento pode chegar antes de sabermos a qual pagamento pertence.
        builder.Property(x => x.IdPagamento).HasColumnName("id_pagamento");

        // Idempotencia de webhook feita no BANCO: a reentrega vira 23505, traduzido em 200.
        builder.Property(x => x.ProviderEventId)
            .HasColumnName("provider_event_id").HasMaxLength(160).IsRequired();

        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(80).IsRequired();

        // Guardado exatamente como chegou.
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.RecebidoEm)
            .HasColumnName("recebido_em").HasColumnType("timestamp without time zone").IsRequired();

        // Null enquanto na fila do worker — o webhook grava e responde rapido.
        builder.Property(x => x.ProcessadoEm)
            .HasColumnName("processado_em").HasColumnType("timestamp without time zone");

        builder.Property(x => x.Erro).HasColumnName("erro").HasColumnType("text");

        builder.HasIndex(x => x.ProviderEventId)
            .IsUnique()
            .HasDatabaseName("ux_pagamentos_eventos_provider_event_id");

        // O evento cru sobrevive por auditoria mesmo se o pagamento sumir: Restrict.
        builder.HasOne(x => x.Pagamento)
            .WithMany(x => x.Eventos)
            .HasForeignKey(x => x.IdPagamento)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
