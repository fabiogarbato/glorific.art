using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class ColecaoRepository : BaseRepository<Colecao>, IColecaoRepository
{
    public ColecaoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Colecao?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Query()
            .Include(c => c.MidiaCapa)
            .Include(c => c.MidiaBanner)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public Task<bool> SlugEmUsoAsync(
        string slug,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query().AnyAsync(
            c => c.Slug == slug && (idIgnorar == null || c.Id != idIgnorar),
            cancellationToken);

    /// <summary>
    /// Habilitadas e dentro da janela DataInicio/DataFim. E o que faz o drop agendado funcionar:
    /// a colecao e cadastrada com antecedencia e entra na vitrine sozinha na virada da hora,
    /// sem ninguem precisar apertar um botao na madrugada. Limite nulo significa "sem limite".
    /// </summary>
    public async Task<IReadOnlyList<Colecao>> ObterVigentesAsync(
        DateTime agoraUtc,
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(c => c.Habilitado
                        && (c.DataInicio == null || c.DataInicio <= agoraUtc)
                        && (c.DataFim == null || c.DataFim >= agoraUtc))
            .Include(c => c.MidiaCapa)
            .Include(c => c.MidiaBanner)
            .OrderByDescending(c => c.Destaque)
            .ThenBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Colecao>> ObterDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default) =>
        await Contexto.ProdutosColecoes
            .AsNoTracking()
            .Where(pc => pc.IdProduto == idProduto)
            .OrderBy(pc => pc.Ordem)
            .Select(pc => pc.Colecao)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Mexe na tabela de juncao, que nao tem repositorio proprio por nao ser agregado.
    /// Revincular so muda a ordem: inserir de novo estouraria o unico (produto, colecao).
    /// Nao salva — quem salva e o caso de uso.
    /// </summary>
    public async Task VincularProdutoAsync(
        int idColecao,
        int idProduto,
        int ordem,
        CancellationToken cancellationToken = default)
    {
        var existente = await Contexto.ProdutosColecoes
            .FirstOrDefaultAsync(
                pc => pc.IdColecao == idColecao && pc.IdProduto == idProduto,
                cancellationToken);

        if (existente is not null)
        {
            existente.Ordem = ordem;
            return;
        }

        await Contexto.ProdutosColecoes.AddAsync(
            new ProdutoColecao { IdColecao = idColecao, IdProduto = idProduto, Ordem = ordem },
            cancellationToken);
    }

    public async Task DesvincularProdutoAsync(
        int idColecao,
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        var vinculo = await Contexto.ProdutosColecoes
            .FirstOrDefaultAsync(
                pc => pc.IdColecao == idColecao && pc.IdProduto == idProduto,
                cancellationToken);

        if (vinculo is not null)
            Contexto.ProdutosColecoes.Remove(vinculo);
    }
}
