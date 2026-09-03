using Glorific.Domain.Entities.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Guid com hifens, um unico formato em todo o sistema.
        builder.Property(x => x.Uuid).HasColumnName("uuid").HasMaxLength(36).IsRequired();

        // Normalizado em minusculas antes de gravar — senao o mesmo e-mail entra duas vezes.
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();

        builder.Property(x => x.EmailVerificado).HasColumnName("email_verificado").HasDefaultValue(false);
        builder.Property(x => x.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(180);
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(20);

        // Nullable de proposito: quem entrou por Google nunca definiu senha.
        builder.Property(x => x.SenhaHash).HasColumnName("senha_hash").HasMaxLength(255);

        builder.Property(x => x.FotoUrl).HasColumnName("foto_url").HasColumnType("text");
        builder.Property(x => x.DataNascimento)
            .HasColumnName("data_nascimento").HasColumnType("timestamp without time zone");
        builder.Property(x => x.AceitaMarketing).HasColumnName("aceita_marketing").HasDefaultValue(false);
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);
        builder.Property(x => x.UltimoLoginEm)
            .HasColumnName("ultimo_login_em").HasColumnType("timestamp without time zone");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_usuarios_email");
        builder.HasIndex(x => x.Uuid).IsUnique().HasDatabaseName("ux_usuarios_uuid");

        // Indice PARCIAL: CPF e opcional, mas quando existe e unico. Sem o filtro, o segundo
        // usuario sem CPF colidiria com o primeiro no NULL.
        builder.HasIndex(x => x.Cpf)
            .IsUnique()
            .HasFilter("cpf IS NOT NULL")
            .HasDatabaseName("ux_usuarios_cpf");
    }
}
