using Glorific.Domain.Entities.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class MovimentacaoEstoqueConfiguration : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> builder)
    {
        builder.ToTable("movimentacoes_estoque");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdVariacao).HasColumnName("id_variacao");
        builder.Property(x => x.IdMovimento).HasColumnName("id_movimento");

        // Sinalizada: positiva entrada, negativa saida.
        builder.Property(x => x.Quantidade).HasColumnName("quantidade");

        // Antes/Depois na propria linha: audita divergencia sem replay do ledger inteiro.
        builder.Property(x => x.QuantidadeAntes).HasColumnName("quantidade_antes");
        builder.Property(x => x.QuantidadeDepois).HasColumnName("quantidade_depois");

        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(400);

        builder.Property(x => x.DataMovimentacao)
            .HasColumnName("data_movimentacao").HasColumnType("timestamp without time zone").IsRequired();

        // Ledger imutavel: nada aqui pode ser levado embora por delete de outra tabela.
        builder.HasOne(x => x.Variacao)
            .WithMany()
            .HasForeignKey(x => x.IdVariacao)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Movimento)
            .WithMany(x => x.Movimentacoes)
            .HasForeignKey(x => x.IdMovimento)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pedido)
            .WithMany()
            .HasForeignKey(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
