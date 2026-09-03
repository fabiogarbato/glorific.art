using Glorific.Domain.Entities.Social;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class AvaliacaoRepository : BaseRepository<Avaliacao>, IAvaliacaoRepository
{
    public AvaliacaoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Somente aprovadas: a vitrine nunca ve avaliacao pendente nem rejeitada. O filtro fica na
    /// consulta, e nao no chamador, porque esquecer o Where uma vez publica moderacao inteira.
    /// Devolve IQueryable para a listagem paginar server-side.
    /// </summary>
    public IQueryable<Avaliacao> QueryAprovadasDoProduto(int idProduto) =>
        Query()
            .Where(a => a.IdProduto == idProduto && a.Status == StatusAvaliacao.Aprovada)
            .Include(a => a.Usuario)
            .Include(a => a.Midias.OrderBy(m => m.Ordem)).ThenInclude(m => m.Midia)
            .OrderByDescending(a => a.DataCriacao)
            .ThenByDescending(a => a.Id);

    /// <summary>Fila de moderacao do painel, da mais antiga para a mais nova.</summary>
    public IQueryable<Avaliacao> QueryPendentes() =>
        Query()
            .Where(a => a.Status == StatusAvaliacao.Pendente)
            .Include(a => a.Usuario)
            .Include(a => a.Midias.OrderBy(m => m.Ordem)).ThenInclude(m => m.Midia)
            .OrderBy(a => a.DataCriacao)
            .ThenBy(a => a.Id);

    /// <summary>
    /// Uma avaliacao por produto por usuario, em qualquer status: quem teve a review rejeitada
    /// nao reenvia a mesma por baixo.
    /// </summary>
    public Task<bool> ExisteDoUsuarioAsync(
        int idProduto,
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        Query().AnyAsync(a => a.IdProduto == idProduto && a.IdUsuario == idUsuario, cancellationToken);

    /// <summary>
    /// Sustenta o selo de compra verificada e bloqueia review de quem nao comprou.
    ///
    /// IgnoreQueryFilters porque o item pode ser de produto ja desativado — a compra aconteceu,
    /// e o direito de avaliar nao morre quando o catalogo muda.
    /// </summary>
    public Task<bool> ItemPertenceAoUsuarioAsync(
        int idPedidoItem,
        int idUsuario,
        int idProduto,
        CancellationToken cancellationToken = default) =>
        Contexto.PedidoItens
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                i => i.Id == idPedidoItem
                     && i.IdProduto == idProduto
                     && i.Pedido.IdUsuario == idUsuario,
                cancellationToken);

    /// <summary>
    /// Media e total ja agregados no banco, para gravar de volta nos campos denormalizados do
    /// produto. Somente aprovadas — o mesmo criterio de RecalcularNotasAsync, senao a vitrine e
    /// o painel divergem.
    /// </summary>
    public async Task<(decimal? Media, int Total)> ObterResumoAsync(
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        var resumo = await Query()
            .Where(a => a.IdProduto == idProduto && a.Status == StatusAvaliacao.Aprovada)
            .GroupBy(a => a.IdProduto)
            .Select(g => new { Media = (decimal?)g.Average(a => (decimal)a.Nota), Total = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        if (resumo?.Media is null)
            return (null, resumo?.Total ?? 0);

        return (Math.Round(resumo.Media.Value, 2), resumo.Total);
    }

    public async Task AdicionarMidiaAsync(AvaliacaoMidia midia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(midia);
        await Contexto.AvaliacoesMidias.AddAsync(midia, cancellationToken);
    }
}
