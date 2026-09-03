using Glorific.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarrinhoEntity = Glorific.Domain.Entities.Carrinho.Carrinho;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class CarrinhoConfiguration : IEntityTypeConfiguration<CarrinhoEntity>
{
    public void Configure(EntityTypeBuilder<CarrinhoEntity> builder)
    {
        builder.ToTable("carrinhos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Guid com hifens — um unico formato em todo o sistema.
        builder.Property(x => x.Uuid).HasColumnName("uuid").HasMaxLength(36).IsRequired();

        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.ChaveSessao).HasColumnName("chave_sessao").HasMaxLength(120);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.IdCupom).HasColumnName("id_cupom");

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");
        builder.Property(x => x.ExpiraEm)
            .HasColumnName("expira_em").HasColumnType("timestamp without time zone").IsRequired();

        builder.HasIndex(x => x.Uuid).IsUnique().HasDatabaseName("ux_carrinhos_uuid");

        // Indices PARCIAIS: um carrinho ABERTO por usuario e um por sessao anonima.
        // Sem o filtro, o carrinho convertido de ontem impediria o carrinho de hoje.
        // O literal 1 e StatusCarrinho.Aberto — o enum e persistido como int.
        builder.HasIndex(x => x.IdUsuario)
            .IsUnique()
            .HasFilter($"status = {(int)StatusCarrinho.Aberto}")
            .HasDatabaseName("ux_carrinhos_usuario_aberto");

        builder.HasIndex(x => x.ChaveSessao)
            .IsUnique()
            .HasFilter($"status = {(int)StatusCarrinho.Aberto}")
            .HasDatabaseName("ux_carrinhos_chave_sessao_aberto");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Carrinhos)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cupom)
            .WithMany()
            .HasForeignKey(x => x.IdCupom)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
