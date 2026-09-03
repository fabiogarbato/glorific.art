using Glorific.Domain.Entities.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glorific.Infrastructure.Data.Configurations;

public sealed class EnderecoConfiguration : IEntityTypeConfiguration<Endereco>
{
    public void Configure(EntityTypeBuilder<Endereco> builder)
    {
        builder.ToTable("enderecos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario");
        builder.Property(x => x.Apelido).HasColumnName("apelido").HasMaxLength(60);
        builder.Property(x => x.Destinatario).HasColumnName("destinatario").HasMaxLength(180).IsRequired();

        // CPF do destinatario, so digitos: a transportadora exige documento na etiqueta.
        builder.Property(x => x.DocumentoDestinatario)
            .HasColumnName("documento_destinatario").HasMaxLength(14);

        builder.Property(x => x.TelefoneContato)
            .HasColumnName("telefone_contato").HasMaxLength(20).IsRequired();

        // Oito digitos, sem mascara.
        builder.Property(x => x.Cep).HasColumnName("cep").HasMaxLength(8).IsRequired();

        builder.Property(x => x.Logradouro).HasColumnName("logradouro").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Numero).HasColumnName("numero").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Complemento).HasColumnName("complemento").HasMaxLength(120);

        // NOT NULL porque POST /api/cart do Melhor Envio exige district.
        builder.Property(x => x.Bairro).HasColumnName("bairro").HasMaxLength(120).IsRequired();

        builder.Property(x => x.Cidade).HasColumnName("cidade").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Uf).HasColumnName("uf").HasColumnType("char(2)").IsRequired();
        builder.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(2).HasDefaultValue("BR").IsRequired();
        builder.Property(x => x.Principal).HasColumnName("principal").HasDefaultValue(false);
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(x => x.DataAlteracao)
            .HasColumnName("data_alteracao").HasColumnType("timestamp without time zone");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Enderecos)
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
