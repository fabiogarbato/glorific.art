using Glorific.Domain.Entities.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("avaliacoes", t => t.HasCheckConstraint(
            "ck_avaliacoes_nota",
            "nota BETWEEN 1 AND 5"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");

        // Sustenta o selo "compra verificada" e bloqueia review de quem nao comprou.
        builder.Property(x => x.IdPedidoItem).HasColumnName("id_pedido_item");

        builder.Property(x => x.Nota).HasColumnName("nota");
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(160);
        builder.Property(x => x.Comentario).HasColumnName("comentario").HasColumnType("text");

        // Texto: o cliente comprou "M" mesmo que a grade mude depois.
        builder.Property(x => x.TamanhoComprado).HasColumnName("tamanho_comprado").HasMaxLength(10);

        // Altura + peso + caimento e o dado que mais reduz devolucao em moda.
        builder.Property(x => x.AlturaClienteCm).HasColumnName("altura_cliente_cm");
        builder.Property(x => x.PesoClienteKg).HasColumnName("peso_cliente_kg").HasPrecision(5, 2);
        builder.Property(x => x.Caimento).HasColumnName("caimento").HasConversion<int?>();

        builder.Property(x => x.Recomenda).HasColumnName("recomenda");

        // Nasce Pendente por decisao de risco reputacional.
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.MotivoRejeicao).HasColumnName("motivo_rejeicao").HasMaxLength(400);
        builder.Property(x => x.ModeradaPor).HasColumnName("moderada_por");
        builder.Property(x => x.ModeradaEm)
            .HasColumnName("moderada_em").HasColumnType("timestamp without time zone");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();

        // Uma avaliacao por cliente por produto.
        builder.HasIndex(x => new { x.IdProduto, x.IdUsuario })
            .IsUnique()
            .HasDatabaseName("ux_avaliacoes_produto_usuario");

        builder.HasOne(x => x.Produto)
            .WithMany(x => x.Avaliacoes)
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Avaliacoes)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PedidoItem)
            .WithMany()
            .HasForeignKey(x => x.IdPedidoItem)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsuarioModerador)
            .WithMany()
            .HasForeignKey(x => x.ModeradaPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
