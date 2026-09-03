using Glorific.Domain.Entities.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Minusculo e sem espaco: e o valor da claim role no JWT.
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255);

        builder.HasIndex(x => x.Nome).IsUnique().HasDatabaseName("ux_roles_nome");
    }
}
