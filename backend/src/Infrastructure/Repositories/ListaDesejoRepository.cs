using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class ListaDesejoRepository : BaseRepository<ListaDesejoItem>, IListaDesejoRepository
{
    public ListaDesejoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// A lista de desejos e uma vitrine, nao uma lista de ids: vem com produto, capa e estoque
    /// da variacao para o card renderizar sem uma consulta por linha.
    ///
    /// IgnoreQueryFilters de proposito. Produto e ProdutoVariacao tem filtro de soft delete e a
    /// navegacao obrigatoria viria nula quando a peca sai do catalogo — o item sumiria calado.
    /// Mostrar "indisponivel" e o comportamento certo: e exatamente a peca que o cliente quer
    /// saber quando voltar.
    /// </summary>
    public async Task<IReadOnlyList<ListaDesejoItem>> ObterDoUsuarioAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        await Query()
            .IgnoreQueryFilters()
            .Where(l => l.IdUsuario == idUsuario)
            .Include(l => l.Produto)
                .ThenInclude(p => p.Midias.Where(m => m.EhCapa).OrderBy(m => m.Ordem))
                .ThenInclude(m => m.Midia)
            .Include(l => l.Variacao!).ThenInclude(v => v.Estoque)
            .Include(l => l.Variacao!).ThenInclude(v => v.Tamanho)
            .Include(l => l.Variacao!).ThenInclude(v => v.Cor)
            .OrderByDescending(l => l.DataCriacao)
            .ThenByDescending(l => l.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Rastreado: quem procura o item ja vai remove-lo (o coracao e um toggle).
    /// IgnoreQueryFilters para o item de produto desativado ainda poder ser retirado da lista.
    /// </summary>
    public Task<ListaDesejoItem?> ObterItemAsync(
        int idUsuario,
        int idProduto,
        CancellationToken cancellationToken = default) =>
        QueryTracked()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.IdUsuario == idUsuario && l.IdProduto == idProduto,
                cancellationToken);

    /// <summary>
    /// So os ids: alimenta o coracao preenchido na listagem inteira sem uma consulta por card.
    /// </summary>
    public async Task<IReadOnlyList<int>> ObterIdsProdutoDoUsuarioAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        await Query()
            .IgnoreQueryFilters()
            .Where(l => l.IdUsuario == idUsuario)
            .Select(l => l.IdProduto)
            .Distinct()
            .ToListAsync(cancellationToken);
}
