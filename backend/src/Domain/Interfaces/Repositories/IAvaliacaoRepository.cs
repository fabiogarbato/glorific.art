using Glorific.Domain.Entities.Social;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IAvaliacaoRepository : IBaseRepository<Avaliacao>
{
    /// <summary>Somente aprovadas: a vitrine nunca ve avaliacao pendente nem rejeitada.</summary>
    IQueryable<Avaliacao> QueryAprovadasDoProduto(int idProduto);

    IQueryable<Avaliacao> QueryPendentes();

    Task<bool> ExisteDoUsuarioAsync(int idProduto, int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confere se o item do pedido e daquele usuario e daquele produto. E o que sustenta o selo
    /// de compra verificada e bloqueia review de quem nao comprou.
    /// </summary>
    Task<bool> ItemPertenceAoUsuarioAsync(int idPedidoItem, int idUsuario, int idProduto, CancellationToken cancellationToken = default);

    /// <summary>Media e total ja agregados, para gravar de volta nos campos denormalizados do produto.</summary>
    Task<(decimal? Media, int Total)> ObterResumoAsync(int idProduto, CancellationToken cancellationToken = default);

    Task AdicionarMidiaAsync(AvaliacaoMidia midia, CancellationToken cancellationToken = default);
}
