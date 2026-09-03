using Glorific.Domain.Entities.Promocoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CupomConfiguration : IEntityTypeConfiguration<Cupom>
{
    public void Configure(EntityTypeBuilder<Cupom> builder)
    {
        builder.ToTable("cupons");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Sempre maiusculo: o usuario digita como quiser, a normalizacao e nossa.
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255);
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>();

        // Percentual x100 (1250 = 12,50%) ou centavos, conforme o Tipo.
        builder.Property(x => x.Valor).HasColumnName("valor");

        builder.Property(x => x.ValorMinimoPedidoCentavos).HasColumnName("valor_minimo_pedido_centavos");

        // Teto do percentual: "50% OFF" em pedido de dois mil reais nao pode virar prejuizo.
        builder.Property(x => x.DescontoMaximoCentavos).HasColumnName("desconto_maximo_centavos");

        builder.Property(x => x.UsoMaximoTotal).HasColumnName("uso_maximo_total");
        builder.Property(x => x.UsoMaximoPorUsuario).HasColumnName("uso_maximo_por_usuario").HasDefaultValue(1);

        // Incrementado por UPDATE condicional, nunca por leitura seguida de escrita.
        builder.Property(x => x.UsosAtuais).HasColumnName("usos_atuais").HasDefaultValue(0);

        builder.Property(x => x.VigenciaInicio)
            .HasColumnName("vigencia_inicio").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.VigenciaFim)
            .HasColumnName("vigencia_fim").HasColumnType("timestamp without time zone");

        builder.Property(x => x.PrimeiraCompraApenas).HasColumnName("primeira_compra_apenas").HasDefaultValue(false);
        builder.Property(x => x.IdCategoriaRestrita).HasColumnName("id_categoria_restrita");
        builder.Property(x => x.IdColecaoRestrita).HasColumnName("id_colecao_restrita");
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("ux_cupons_codigo");

        builder.HasOne(x => x.CategoriaRestrita)
            .WithMany()
            .HasForeignKey(x => x.IdCategoriaRestrita)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ColecaoRestrita)
            .WithMany()
            .HasForeignKey(x => x.IdColecaoRestrita)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
