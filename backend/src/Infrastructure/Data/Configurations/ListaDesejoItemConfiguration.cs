using Glorific.Domain.Entities.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class ListaDesejoItemConfiguration : IEntityTypeConfiguration<ListaDesejoItem>
{
    public void Configure(EntityTypeBuilder<ListaDesejoItem> builder)
    {
        builder.ToTable("lista_desejo_itens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.IdProduto).HasColumnName("id_produto");

        // Variacao opcional: em moda o cliente favorita a peca antes de escolher o tamanho.
        builder.Property(x => x.IdVariacao).HasColumnName("id_variacao");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();

        // A unicidade e por produto, nao por variacao: favoritar duas vezes a mesma peca nao existe.
        builder.HasIndex(x => new { x.IdUsuario, x.IdProduto })
            .IsUnique()
            .HasDatabaseName("ux_lista_desejo_itens_usuario_produto");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.ListaDesejo)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Produto)
            .WithMany()
            .HasForeignKey(x => x.IdProduto)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variacao)
            .WithMany()
            .HasForeignKey(x => x.IdVariacao)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
