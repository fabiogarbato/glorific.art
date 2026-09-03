using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IProdutoRepository : IBaseRepository<Produto>
{
    Task<Produto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Com variacoes, midias e tabela de medidas: o que a pagina de produto precisa numa ida so.</summary>
    Task<Produto?> ObterCompletoAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> SlugEmUsoAsync(string slug, int? idIgnorar = null, CancellationToken cancellationToken = default);

    Task<bool> SkuBaseEmUsoAsync(string skuBase, int? idIgnorar = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Somente produtos com ao menos uma variacao ativa e disponivel. Sem este filtro o catalogo
    /// mostra peca que nao pode ser comprada em nenhum tamanho.
    /// </summary>
    IQueryable<Produto> QueryDisponiveis();

    /// <summary>
    /// Ignora o filtro de soft delete. Tela de historico de pedido PRECISA enxergar produto
    /// desativado, senao o recibo antigo aparece quebrado.
    /// </summary>
    Task<Produto?> ObterParaHistoricoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Recalcula NotaMedia e TotalAvaliacoes denormalizados depois de moderar avaliacao.</summary>
    Task RecalcularNotasAsync(int idProduto, CancellationToken cancellationToken = default);
}
