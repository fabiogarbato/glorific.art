using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Clientes;

/// <summary>
/// Item da lista de desejos.
///
/// A variacao e OPCIONAL por decisao de produto: em moda o cliente favorita a peca ("quero este
/// vestido") antes de decidir o tamanho, e exigir a variacao esvaziaria o recurso. Quando ela
/// vem informada, e o que permite avisar "voltou ao estoque" no tamanho certo.
/// </summary>
public sealed record ListaDesejoCreateDto : CreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Produto invalido.")]
    public int IdProduto { get; init; }

    public int? IdVariacao { get; init; }
}
