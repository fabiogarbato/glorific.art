using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class CorRepository : BaseRepository<Cor>, ICorRepository
{
    public CorRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Cor?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Query()
            .Include(c => c.MidiaSwatch)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public Task<bool> SlugEmUsoAsync(
        string slug,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query().AnyAsync(
            c => c.Slug == slug && (idIgnorar == null || c.Id != idIgnorar),
            cancellationToken);

    /// <summary>Ordem de exibicao do seletor, definida pelo campo Ordem e nao pelo nome.</summary>
    public async Task<IReadOnlyList<Cor>> ObterAtivasOrdenadasAsync(CancellationToken cancellationToken = default) =>
        await Query()
            .Where(c => c.Ativo)
            .Include(c => c.MidiaSwatch)
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Cores que aparecem na vitrine de um produto, para montar o seletor de swatch.
    ///
    /// Sai das variacoes ativas — cor cadastrada sem variacao nenhuma nao vira bolinha clicavel
    /// que leva a lugar nenhum. Distinct porque a mesma cor aparece uma vez por tamanho.
    /// </summary>
    public async Task<IReadOnlyList<Cor>> ObterDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default) =>
        await Contexto.ProdutoVariacoes
            .AsNoTracking()
            .Where(v => v.IdProduto == idProduto && v.Ativo)
            .Select(v => v.Cor)
            .Distinct()
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);
}
