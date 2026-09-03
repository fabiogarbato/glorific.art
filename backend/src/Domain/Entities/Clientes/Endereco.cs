using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Entities.Clientes;

/// <summary>
/// Endereco ACHATADO de proposito. O repo de referencia normalizava
/// Pais -> Estado -> Cidade -> EnderecoEntrega -> EntregaCliente (cinco tabelas), e o preco
/// aparecia num get-or-create transacional de tres niveis a cada CEP novo, mais um indice
/// unico de cidade que quebrava com variacao de acentuacao. Endereco de entrega e snapshot
/// de texto, nao entidade de dominio: a normalizacao nao paga o proprio custo.
/// </summary>
public class Endereco : BaseEntity, IAuditable
{
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Rotulo do cliente: "Casa", "Trabalho".</summary>
    public string? Apelido { get; set; }

    public required string Destinatario { get; set; }

    /// <summary>
    /// CPF do destinatario, so digitos. A transportadora exige documento na compra da etiqueta;
    /// sem ele a etiqueta falha DEPOIS de o cliente ja ter pago.
    /// </summary>
    public string? DocumentoDestinatario { get; set; }

    public required string TelefoneContato { get; set; }

    /// <summary>Oito digitos, sem mascara.</summary>
    public required string Cep { get; set; }

    public required string Logradouro { get; set; }
    public required string Numero { get; set; }
    public string? Complemento { get; set; }

    /// <summary>
    /// Obrigatorio porque o Melhor Envio exige district em POST /api/cart. O repo de referencia
    /// descobriu isso em producao e ate hoje faz backfill com o literal "Centro".
    /// </summary>
    public required string Bairro { get; set; }

    public required string Cidade { get; set; }

    /// <summary>Sigla de dois caracteres, maiuscula.</summary>
    public required string Uf { get; set; }

    public string Pais { get; set; } = "BR";

    public bool Principal { get; set; }

    /// <summary>Soft delete: o pedido antigo guarda o proprio snapshot, mas o cliente perde a lista.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }
}
