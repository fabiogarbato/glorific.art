using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IColecaoRepository : IBaseRepository<Colecao>
{
    Task<Colecao?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugEmUsoAsync(string slug, int? idIgnorar = null, CancellationToken cancellationToken = default);

    /// <summary>Habilitadas e dentro da janela DataInicio/DataFim: e o que faz o drop agendado funcionar.</summary>
    Task<IReadOnlyList<Colecao>> ObterVigentesAsync(DateTime agoraUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Colecao>> ObterDoProdutoAsync(int idProduto, CancellationToken cancellationToken = default);

    /// <summary>Mexe na tabela de juncao, que nao tem repositorio proprio por nao ser agregado.</summary>
    Task VincularProdutoAsync(int idColecao, int idProduto, int ordem, CancellationToken cancellationToken = default);

    Task DesvincularProdutoAsync(int idColecao, int idProduto, CancellationToken cancellationToken = default);
}
