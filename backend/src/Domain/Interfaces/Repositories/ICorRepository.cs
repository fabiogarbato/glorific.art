using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ICorRepository : IBaseRepository<Cor>
{
    Task<Cor?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugEmUsoAsync(string slug, int? idIgnorar = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Cor>> ObterAtivasOrdenadasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cores que aparecem na vitrine de um produto, para montar o seletor de swatch.</summary>
    Task<IReadOnlyList<Cor>> ObterDoProdutoAsync(int idProduto, CancellationToken cancellationToken = default);
}
