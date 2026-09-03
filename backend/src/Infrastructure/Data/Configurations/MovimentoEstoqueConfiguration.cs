using Glorific.Domain.Entities.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> builder)
    {
        builder.ToTable("movimentos_estoque");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // O valor casa exatamente com MovimentoEstoqueKeys: e a chave de resolucao em runtime.
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255);

        // Positivo entrada, negativo saida, zero neutro (reserva e liberacao).
        builder.Property(x => x.Sinal).HasColumnName("sinal");

        // O nome e a chave de lookup usada por MovimentoEstoqueKeys. Sem UNIQUE, um seed
        // rodando duas vezes duplica a linha e a resolucao por nome vira nao-deterministica.
        builder.HasIndex(x => x.Nome).IsUnique().HasDatabaseName("ux_movimentos_estoque_nome");
    }
}
