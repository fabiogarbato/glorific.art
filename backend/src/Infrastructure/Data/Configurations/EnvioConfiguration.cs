using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class EnvioConfiguration : IEntityTypeConfiguration<Envio>
{
    public void Configure(EntityTypeBuilder<Envio> builder)
    {
        builder.ToTable("envios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdPedido).HasColumnName("id_pedido");

        // Uuid da etiqueta no Melhor Envio. Null enquanto nada foi comprado la.
        builder.Property(x => x.MeOrderId).HasColumnName("me_order_id").HasMaxLength(80);

        builder.Property(x => x.IdServico).HasColumnName("id_servico");
        builder.Property(x => x.NomeServico).HasColumnName("nome_servico").HasMaxLength(120);
        builder.Property(x => x.NomeTransportadora).HasColumnName("nome_transportadora").HasMaxLength(120);

        // Cotado e o que foi mostrado ao cliente; comprado e o custo real da carteira do ME.
        // Os dois separados sao o que permite medir margem de frete.
        builder.Property(x => x.ValorCotadoCentavos).HasColumnName("valor_cotado_centavos");
        builder.Property(x => x.ValorCompradoCentavos).HasColumnName("valor_comprado_centavos");

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.CodigoRastreio).HasColumnName("codigo_rastreio").HasMaxLength(60);
        builder.Property(x => x.UrlEtiqueta).HasColumnName("url_etiqueta").HasColumnType("text");
        builder.Property(x => x.ChaveNfe).HasColumnName("chave_nfe").HasMaxLength(44);

        builder.Property(x => x.Tentativas).HasColumnName("tentativas").HasDefaultValue(0);

        // Truncado: stack trace do parceiro nao pode estourar a linha.
        builder.Property(x => x.UltimoErro).HasColumnName("ultimo_erro").HasMaxLength(2000);

        builder.Property(x => x.ProximaTentativaEm)
            .HasColumnName("proxima_tentativa_em").HasColumnType("timestamp without time zone");

        builder.Property(x => x.RawUltimaResposta)
            .HasColumnName("raw_ultima_resposta").HasColumnType("jsonb");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        // Concorrencia otimista com o xmin do Postgres: resolve a corrida entre o
        // EnvioProcessor e a contratacao manual do admin sobre o mesmo envio.
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");

        // A unicidade aqui, e nao um if no codigo, e o que impede comprar duas etiquetas
        // para o mesmo pedido.
        builder.HasIndex(x => x.IdPedido).IsUnique().HasDatabaseName("ux_envios_pedido");
        builder.HasIndex(x => x.MeOrderId).HasDatabaseName("ix_envios_me_order_id");

        // A entidade tambem e a FILA do worker: este e o indice que o GetPendentesAsync usa.
        builder.HasIndex(x => new { x.Status, x.ProximaTentativaEm })
            .HasDatabaseName("ix_envios_status_proxima_tentativa");

        builder.HasOne(x => x.Pedido)
            .WithOne(x => x.Envio)
            .HasForeignKey<Envio>(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
