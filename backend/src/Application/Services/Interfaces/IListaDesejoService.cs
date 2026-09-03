using Glorific.Application.DTO.Clientes;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Lista de desejos do cliente.
///
/// Nao herda IGenericService porque a chave de negocio aqui nao e o Id da linha: e o par
/// (usuario, produto). Remover por Id da linha exigiria que o front guardasse esse id e abriria a
/// porta para alguem tentar apagar o item de outra pessoa — todo metodo desta interface recebe o
/// usuario e filtra por ele NA CONSULTA, nunca depois em memoria.
///
/// A listagem nao e paginada de proposito: lista de desejos e curta por natureza e a tela mostra
/// tudo de uma vez. Paginar aqui seria complexidade sem beneficio.
/// </summary>
public interface IListaDesejoService
{
    Task<IReadOnlyList<ListaDesejoItemResponseDto>> ListarAsync(
        int idUsuario,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// So os ids de produto. E o que alimenta o coracao preenchido na vitrine inteira sem uma
    /// consulta por card.
    /// </summary>
    Task<IReadOnlyList<int>> ObterIdsProdutoAsync(
        int idUsuario,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotente: favoritar de novo devolve o item que ja existia, sem erro.</summary>
    Task<ListaDesejoItemResponseDto> AdicionarAsync(
        int idUsuario,
        ListaDesejoCreateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>404 quando o produto nao esta na lista DAQUELE usuario.</summary>
    Task RemoverAsync(int idUsuario, int idProduto, CancellationToken cancellationToken = default);

    /// <summary>
    /// O coracao da vitrine e um botao so. Devolve true quando o item passou a fazer parte da
    /// lista e false quando saiu — sem exigir do front saber o estado anterior.
    /// </summary>
    Task<bool> AlternarAsync(
        int idUsuario,
        ListaDesejoCreateDto dto,
        CancellationToken cancellationToken = default);
}
