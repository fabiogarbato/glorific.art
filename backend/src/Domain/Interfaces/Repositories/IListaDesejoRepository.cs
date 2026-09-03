using Glorific.Domain.Entities.Clientes;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IListaDesejoRepository : IBaseRepository<ListaDesejoItem>
{
    /// <summary>Com produto, midia de capa e disponibilidade: a lista e uma vitrine, nao uma lista de ids.</summary>
    Task<IReadOnlyList<ListaDesejoItem>> ObterDoUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    Task<ListaDesejoItem?> ObterItemAsync(int idUsuario, int idProduto, CancellationToken cancellationToken = default);

    /// <summary>Alimenta o coracao preenchido na listagem sem uma consulta por card.</summary>
    Task<IReadOnlyList<int>> ObterIdsProdutoDoUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);
}
