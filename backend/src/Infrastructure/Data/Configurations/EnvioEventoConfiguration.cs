using Glorific.Domain.Entities.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class EnvioEventoConfiguration : IEntityTypeConfiguration<EnvioEvento>
{
    public void Configure(EntityTypeBuilder<EnvioEvento> builder)
    {
        builder.ToTable("envios_eventos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdEnvio).HasColumnName("id_envio");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(400);
        builder.Property(x => x.Local).HasColumnName("local").HasMaxLength(200);

        // Ocorrido e o instante da transportadora; registrado e quando nos soubemos.
        // A timeline do cliente mostra o primeiro; o suporte investiga com o segundo.
        builder.Property(x => x.OcorridoEm)
            .HasColumnName("ocorrido_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.RegistradoEm)
            .HasColumnName("registrado_em").HasColumnType("timestamp without time zone").IsRequired();

        builder.HasOne(x => x.Envio)
            .WithMany(x => x.Eventos)
            .HasForeignKey(x => x.IdEnvio)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
