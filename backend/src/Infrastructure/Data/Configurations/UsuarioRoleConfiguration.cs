using Glorific.Domain.Entities.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class UsuarioRoleConfiguration : IEntityTypeConfiguration<UsuarioRole>
{
    public void Configure(EntityTypeBuilder<UsuarioRole> builder)
    {
        builder.ToTable("usuarios_roles");

        // A identidade da linha e o proprio par: PK sintetica permitiria gravar o papel duas vezes.
        builder.HasKey(x => new { x.IdUsuario, x.IdRole });

        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.IdRole).HasColumnName("id_role");

        builder.Property(x => x.ConcedidaEm)
            .HasColumnName("concedida_em").HasColumnType("timestamp without time zone").IsRequired();

        // Responde "quem promoveu este usuario a admin".
        builder.Property(x => x.ConcedidaPor).HasColumnName("concedida_por");

        // Vinculo pertence ao usuario: desativar conta e apagar vinculo andam juntos.
        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Roles)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        // O papel e catalogo: apagar "gerente" nao pode arrastar vinculo em silencio.
        builder.HasOne(x => x.Role)
            .WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.IdRole)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsuarioConcedente)
            .WithMany()
            .HasForeignKey(x => x.ConcedidaPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
