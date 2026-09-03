using Glorific.Domain.Entities.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ConfiguracaoLojaConfiguration : IEntityTypeConfiguration<ConfiguracaoLoja>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoLoja> builder)
    {
        builder.ToTable("configuracoes_loja");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.FreteGratisAcimaDeCentavos).HasColumnName("frete_gratis_acima_de_centavos");
        builder.Property(x => x.PrazoManuseioDias).HasColumnName("prazo_manuseio_dias").HasDefaultValue(2);

        // CEP de origem das cotacoes, so digitos.
        builder.Property(x => x.CepOrigem).HasColumnName("cep_origem").HasMaxLength(8).IsRequired();

        builder.Property(x => x.PoliticaTrocaDias).HasColumnName("politica_troca_dias").HasDefaultValue(7);
        builder.Property(x => x.PedidoMinimoCentavos).HasColumnName("pedido_minimo_centavos");
        builder.Property(x => x.ExibirEstoqueBaixo).HasColumnName("exibir_estoque_baixo").HasDefaultValue(false);
        builder.Property(x => x.LimiteEstoqueBaixo).HasColumnName("limite_estoque_baixo").HasDefaultValue(3);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");
    }
}
