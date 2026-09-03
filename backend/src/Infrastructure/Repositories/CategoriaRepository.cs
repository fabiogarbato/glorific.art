using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class CategoriaRepository : BaseRepository<Categoria>, ICategoriaRepository
{
    public CategoriaRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Categoria?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Query()
            .Include(c => c.MidiaCapa)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public Task<bool> SlugEmUsoAsync(
        string slug,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query().AnyAsync(
            c => c.Slug == slug && (idIgnorar == null || c.Id != idIgnorar),
            cancellationToken);

    /// <summary>
    /// Raizes com as filhas ja carregadas: o menu do site inteiro em UMA consulta, em vez de
    /// uma ida ao banco por nivel. A ordem e a de exibicao (campo Ordem), nunca alfabetica.
    /// </summary>
    public async Task<IReadOnlyList<Categoria>> ObterArvoreAsync(
        bool somenteHabilitadas = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Categoria> consulta;

        if (somenteHabilitadas)
        {
            consulta = Query()
                .Where(c => c.Habilitado)
                .Include(c => c.Filhas.Where(f => f.Habilitado).OrderBy(f => f.Ordem).ThenBy(f => f.Nome))
                .Include(c => c.MidiaCapa);
        }
        else
        {
            consulta = Query()
                .Include(c => c.Filhas.OrderBy(f => f.Ordem).ThenBy(f => f.Nome))
                .Include(c => c.MidiaCapa);
        }

        return await consulta
            .Where(c => c.IdCategoriaPai == null)
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Barra a exclusao antes de o Restrict do banco virar erro cru na tela do admin.
    ///
    /// IgnoreQueryFilters nos produtos: produto desativado continua apontando para a categoria
    /// e a FK continua impedindo o delete, mesmo que ele nao apareca em lugar nenhum.
    /// </summary>
    public async Task<bool> PossuiVinculosAsync(
        int idCategoria,
        CancellationToken cancellationToken = default)
    {
        var temFilhas = await Query().AnyAsync(c => c.IdCategoriaPai == idCategoria, cancellationToken);

        if (temFilhas)
            return true;

        return await Contexto.Produtos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.IdCategoria == idCategoria, cancellationToken);
    }
}
