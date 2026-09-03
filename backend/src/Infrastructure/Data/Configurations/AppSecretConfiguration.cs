using Glorific.Domain.Entities.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class AppSecretConfiguration : IEntityTypeConfiguration<AppSecret>
{
    public void Configure(EntityTypeBuilder<AppSecret> builder)
    {
        builder.ToTable("app_secrets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // A coluna e config_key porque "key" e palavra reservada no PostgreSQL.
        builder.Property(x => x.Chave).HasColumnName("config_key").HasMaxLength(120).IsRequired();

        // Texto cifrado AES-256-GCM. O valor em claro nunca e persistido nem logado.
        builder.Property(x => x.ValorCriptografado)
            .HasColumnName("valor_criptografado").HasColumnType("text").IsRequired();

        builder.Property(x => x.EhSegredo).HasColumnName("eh_segredo").HasDefaultValue(true);
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.Chave).IsUnique().HasDatabaseName("ux_app_secrets_config_key");
    }
}
