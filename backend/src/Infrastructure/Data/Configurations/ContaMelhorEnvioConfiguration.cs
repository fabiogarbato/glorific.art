using Glorific.Domain.Entities.Integracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ContaMelhorEnvioConfiguration : IEntityTypeConfiguration<ContaMelhorEnvio>
{
    public void Configure(EntityTypeBuilder<ContaMelhorEnvio> builder)
    {
        builder.ToTable("contas_melhor_envio");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ContaId).HasColumnName("conta_id").HasMaxLength(60).IsRequired();
        builder.Property(x => x.AccessToken).HasColumnName("access_token");
        builder.Property(x => x.RefreshToken).HasColumnName("refresh_token");
        builder.Property(x => x.TipoToken).HasColumnName("tipo_token").HasMaxLength(30);
        builder.Property(x => x.Escopo).HasColumnName("escopo");
        builder.Property(x => x.ExpiraEmUtc)
            .HasColumnName("expira_em_utc").HasColumnType("timestamp without time zone");
        builder.Property(x => x.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.ContaId).IsUnique();
    }
}
