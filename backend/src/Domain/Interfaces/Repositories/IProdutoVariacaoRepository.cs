using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IProdutoVariacaoRepository : IBaseRepository<ProdutoVariacao>
{
    Task<ProdutoVariacao?> ObterPorSkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProdutoVariacao>> ObterPorProdutoAsync(int idProduto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Com produto, tamanho, cor e estoque carregados. O checkout monta o snapshot do item do
    /// pedido a partir daqui, numa consulta so para o carrinho inteiro.
    /// </summary>
    Task<IReadOnlyList<ProdutoVariacao>> ObterParaCheckoutAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Protege o unico (produto, tamanho, cor) antes de o banco devolver violacao crua.</summary>
    Task<bool> CombinacaoEmUsoAsync(int idProduto, int idTamanho, int idCor, int? idIgnorar = null, CancellationToken cancellationToken = default);

    Task<bool> SkuEmUsoAsync(string sku, int? idIgnorar = null, CancellationToken cancellationToken = default);
}
