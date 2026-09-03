using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Numero e o identificador humano (GA-2026-000137); Uuid e o da URL publica.
        builder.Property(x => x.Numero).HasColumnName("numero").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Uuid).HasColumnName("uuid").HasMaxLength(36).IsRequired();

        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();

        // Todo valor cobrado esta gravado: nada e recalculado na leitura.
        builder.Property(x => x.SubtotalCentavos).HasColumnName("subtotal_centavos");
        builder.Property(x => x.DescontoCupomCentavos).HasColumnName("desconto_cupom_centavos");
        builder.Property(x => x.FreteCentavos).HasColumnName("frete_centavos");
        builder.Property(x => x.TotalCentavos).HasColumnName("total_centavos");

        builder.Property(x => x.IdCupom).HasColumnName("id_cupom");
        builder.Property(x => x.CodigoCupomSnapshot).HasColumnName("codigo_cupom_snapshot").HasMaxLength(40);

        builder.Property(x => x.IdServicoFrete).HasColumnName("id_servico_frete");
        builder.Property(x => x.TransportadoraFrete).HasColumnName("transportadora_frete").HasMaxLength(120);
        builder.Property(x => x.ServicoFrete).HasColumnName("servico_frete").HasMaxLength(120);
        builder.Property(x => x.PrazoFreteDias).HasColumnName("prazo_frete_dias");

        builder.Property(x => x.ObservacaoCliente).HasColumnName("observacao_cliente").HasColumnType("text");
        builder.Property(x => x.PesoTotalGramas).HasColumnName("peso_total_gramas");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataPagamento)
            .HasColumnName("data_pagamento").HasColumnType("timestamp without time zone");
        builder.Property(x => x.DataEnvio)
            .HasColumnName("data_envio").HasColumnType("timestamp without time zone");
        builder.Property(x => x.DataEntrega)
            .HasColumnName("data_entrega").HasColumnType("timestamp without time zone");
        builder.Property(x => x.DataCancelamento)
            .HasColumnName("data_cancelamento").HasColumnType("timestamp without time zone");
        builder.Property(x => x.MotivoCancelamento).HasColumnName("motivo_cancelamento").HasMaxLength(400);

        // Endereco CONGELADO no pedido. Owned type com prefixo entrega_: o cliente edita ou
        // apaga o endereco depois, e o pedido de seis meses atras nao pode mudar de destino.
        builder.OwnsOne(x => x.EnderecoEntrega, entrega =>
        {
            entrega.Property(e => e.Destinatario)
                .HasColumnName("entrega_destinatario").HasMaxLength(180).IsRequired();
            entrega.Property(e => e.DocumentoDestinatario)
                .HasColumnName("entrega_documento_destinatario").HasMaxLength(14).IsRequired();
            entrega.Property(e => e.TelefoneContato)
                .HasColumnName("entrega_telefone_contato").HasMaxLength(20).IsRequired();
            entrega.Property(e => e.Cep)
                .HasColumnName("entrega_cep").HasMaxLength(8).IsRequired();
            entrega.Property(e => e.Logradouro)
                .HasColumnName("entrega_logradouro").HasMaxLength(200).IsRequired();
            entrega.Property(e => e.Numero)
                .HasColumnName("entrega_numero").HasMaxLength(20).IsRequired();
            entrega.Property(e => e.Complemento)
                .HasColumnName("entrega_complemento").HasMaxLength(120);
            // Nunca vazio: e o district obrigatorio do Melhor Envio.
            entrega.Property(e => e.Bairro)
                .HasColumnName("entrega_bairro").HasMaxLength(120).IsRequired();
            entrega.Property(e => e.Cidade)
                .HasColumnName("entrega_cidade").HasMaxLength(120).IsRequired();
            entrega.Property(e => e.Uf)
                .HasColumnName("entrega_uf").HasColumnType("char(2)").IsRequired();
            entrega.Property(e => e.Pais)
                .HasColumnName("entrega_pais").HasMaxLength(2).HasDefaultValue("BR").IsRequired();
        });

        builder.Navigation(x => x.EnderecoEntrega).IsRequired();

        builder.HasIndex(x => x.Numero).IsUnique().HasDatabaseName("ux_pedidos_numero");
        builder.HasIndex(x => x.Uuid).IsUnique().HasDatabaseName("ux_pedidos_uuid");

        // "Meus pedidos" e sempre por usuario, do mais recente para o mais antigo.
        builder.HasIndex(x => new { x.IdUsuario, x.DataCriacao })
            .HasDatabaseName("ix_pedidos_usuario_data_criacao");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Pedidos)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cupom)
            .WithMany(x => x.Pedidos)
            .HasForeignKey(x => x.IdCupom)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
