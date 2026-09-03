using Glorific.Domain.Entities.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class LoginExternoConfiguration : IEntityTypeConfiguration<LoginExterno>
{
    public void Configure(EntityTypeBuilder<LoginExterno> builder)
    {
        builder.ToTable("logins_externos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");

        // Minusculo: google, apple.
        builder.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(30).IsRequired();

        // A identidade do Google e o claim sub, imutavel — nunca o e-mail.
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasMaxLength(255).IsRequired();

        // Guardado so para auditoria; nunca usado para casar a conta.
        builder.Property(x => x.EmailNoProvedor)
            .HasColumnName("email_no_provedor").HasMaxLength(255).IsRequired();

        builder.Property(x => x.DataVinculo)
            .HasColumnName("data_vinculo").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.UltimoUsoEm)
            .HasColumnName("ultimo_uso_em").HasColumnType("timestamp without time zone");

        builder.HasIndex(x => new { x.Provedor, x.SubjectId })
            .IsUnique()
            .HasDatabaseName("ux_logins_externos_provedor_subject");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.LoginsExternos)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
