namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// Endereco de entrega CONGELADO no pedido (owned type, colunas com prefixo entrega_ na tabela
/// de pedidos). Sem Id e sem BaseEntity de proposito: nao tem ciclo de vida proprio, existe
/// enquanto o pedido existir.
///
/// Por que copiar em vez de referenciar enderecos: o cliente edita ou apaga o endereco depois,
/// e o pedido de seis meses atras nao pode passar a dizer que foi entregue no endereco novo.
/// E tambem o que a nota fiscal e a etiqueta precisam reproduzir exatamente como foi despachado.
/// </summary>
public class PedidoEnderecoSnapshot
{
    public required string Destinatario { get; set; }

    /// <summary>CPF do destinatario, so digitos — exigido pela transportadora na etiqueta.</summary>
    public required string DocumentoDestinatario { get; set; }

    public required string TelefoneContato { get; set; }

    public required string Cep { get; set; }
    public required string Logradouro { get; set; }
    public required string Numero { get; set; }
    public string? Complemento { get; set; }

    /// <summary>Nunca vazio: e o district obrigatorio do Melhor Envio.</summary>
    public required string Bairro { get; set; }

    public required string Cidade { get; set; }
    public required string Uf { get; set; }
    public string Pais { get; set; } = "BR";
}
