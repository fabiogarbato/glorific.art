using Glorific.Domain.Entities.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class EstoqueVariacaoConfiguration : IEntityTypeConfiguration<EstoqueVariacao>
{
    public void Configure(EntityTypeBuilder<EstoqueVariacao> builder)
    {
        // O CHECK e o que impede reserva maior que o fisico mesmo se um caminho de codigo
        // esquecer o WHERE condicional do UPDATE de reserva.
        builder.ToTable("estoques_variacoes", t => t.HasCheckConstraint(
            "ck_estoques_variacoes_quantidades",
            "quantidade >= 0 AND quantidade_reservada >= 0 AND quantidade_reservada <= quantidade"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdVariacao).HasColumnName("id_variacao");

        // Quantidade e o estoque FISICO; reserva e soft. Ver armadilha nº 1 do blueprint.
        builder.Property(x => x.Quantidade).HasColumnName("quantidade").HasDefaultValue(0);
        builder.Property(x => x.QuantidadeReservada).HasColumnName("quantidade_reservada").HasDefaultValue(0);
        builder.Property(x => x.QuantidadeMinima).HasColumnName("quantidade_minima").HasDefaultValue(0);
        builder.Property(x => x.Localizacao).HasColumnName("localizacao").HasMaxLength(120);

        builder.Property(x => x.DataUltimaMovimentacao)
            .HasColumnName("data_ultima_movimentacao").HasColumnType("timestamp without time zone");

        // Disponivel e calculo de leitura do Domain, nao coluna.
        builder.Ignore(x => x.Disponivel);

        // Um estoque por SKU. E o indice, e nao um if no service, que impede a linha duplicada.
        builder.HasIndex(x => x.IdVariacao)
            .IsUnique()
            .HasDatabaseName("ux_estoques_variacoes_variacao");

        builder.HasOne(x => x.Variacao)
            .WithOne(x => x.Estoque)
            .HasForeignKey<EstoqueVariacao>(x => x.IdVariacao)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
