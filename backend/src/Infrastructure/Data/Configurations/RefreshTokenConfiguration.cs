using Glorific.Domain.Entities.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");

        // SHA-256 em hex do token opaco: dump de banco vazado nao vira sessao valida.
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();

        builder.Property(x => x.ExpiraEm)
            .HasColumnName("expira_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.CriadoEm)
            .HasColumnName("criado_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.RevogadoEm)
            .HasColumnName("revogado_em").HasColumnType("timestamp without time zone");

        // Preenchido = este token ja foi usado. Reapresentacao significa roubo.
        builder.Property(x => x.SubstituidoPorHash).HasColumnName("substituido_por_hash").HasMaxLength(64);

        // Amarra a cadeia de rotacoes: a resposta ao reuso e revogar a familia inteira.
        builder.Property(x => x.IdFamilia).HasColumnName("id_familia");

        builder.Property(x => x.IpCriacao).HasColumnName("ip_criacao").HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(400);

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
