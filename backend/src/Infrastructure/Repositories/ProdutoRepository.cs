using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class ProdutoRepository : BaseRepository<Produto>, IProdutoRepository
{
    public ProdutoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Produto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    /// <summary>
    /// Tudo que a pagina de produto precisa numa ida so: variacoes com tamanho, cor e estoque,
    /// galeria ordenada e tabela de medidas com as linhas. Sem isso a tela faz N+1 — uma
    /// consulta por swatch e uma por tamanho.
    /// </summary>
    public Task<Produto?> ObterCompletoAsync(int id, CancellationToken cancellationToken = default) =>
        Query()
            .Include(p => p.Categoria)
            .Include(p => p.Variacoes).ThenInclude(v => v.Tamanho)
            .Include(p => p.Variacoes).ThenInclude(v => v.Cor)
            .Include(p => p.Variacoes).ThenInclude(v => v.Estoque)
            .Include(p => p.Midias.OrderByDescending(m => m.EhCapa).ThenBy(m => m.Ordem))
                .ThenInclude(m => m.Midia)
            .Include(p => p.TabelaMedidas!).ThenInclude(t => t.Linhas.OrderBy(l => l.Ordem))
                .ThenInclude(l => l.Tamanho)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SlugEmUsoAsync(
        string slug,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        // IgnoreQueryFilters: o indice unico nao conhece soft delete. Produto desativado ainda
        // ocupa o slug, e sem isto o cadastro passa na validacao e estoura no banco.
        Query()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Slug == slug && (idIgnorar == null || p.Id != idIgnorar), cancellationToken);

    public Task<bool> SkuBaseEmUsoAsync(
        string skuBase,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.SkuBase == skuBase && (idIgnorar == null || p.Id != idIgnorar), cancellationToken);

    /// <summary>
    /// Vitrine: somente produtos com ao menos uma variacao ativa e com saldo LIVRE.
    ///
    /// Sem o "(quantidade - reservada) > 0" o catalogo mostra peca que nao pode ser comprada em
    /// nenhum tamanho, e o cliente so descobre no carrinho. O filtro de soft delete do Produto e
    /// da ProdutoVariacao ja vem do modelo — aqui e o disponivel que precisa ser explicito.
    /// </summary>
    public IQueryable<Produto> QueryDisponiveis() =>
        Query().Where(p => p.Variacoes.Any(
            v => v.Estoque != null && (v.Estoque.Quantidade - v.Estoque.QuantidadeReservada) > 0));

    /// <summary>
    /// Ignora o filtro de soft delete de proposito: a tela de historico de pedido PRECISA
    /// enxergar produto desativado, senao o recibo antigo aparece quebrado.
    /// </summary>
    public Task<Produto?> ObterParaHistoricoAsync(int id, CancellationToken cancellationToken = default) =>
        Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>
    /// Recalcula NotaMedia e TotalAvaliacoes denormalizados depois de moderar uma avaliacao.
    ///
    /// A agregacao roda no banco e o resultado vai em um UPDATE direto: carregar o produto,
    /// alterar dois campos e salvar disputaria a linha com qualquer edicao de catalogo em curso.
    /// So avaliacao APROVADA conta — pendente e rejeitada nao movem a nota da vitrine.
    ///
    /// ExecuteUpdateAsync nao atualiza o identity map; o produto e desanexado em seguida e quem
    /// precisar da nota nova reconsulta.
    /// </summary>
    public async Task RecalcularNotasAsync(int idProduto, CancellationToken cancellationToken = default)
    {
        var resumo = await Contexto.Avaliacoes
            .AsNoTracking()
            .Where(a => a.IdProduto == idProduto && a.Status == StatusAvaliacao.Aprovada)
            .GroupBy(a => a.IdProduto)
            .Select(g => new { Media = (decimal?)g.Average(a => (decimal)a.Nota), Total = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        // Sem avaliacao aprovada a nota volta a null, e nao a zero: zero estrela e uma nota,
        // ausencia de nota e outra coisa, e a vitrine mostra as duas de forma diferente.
        var media = resumo?.Media is null ? (decimal?)null : Math.Round(resumo.Media.Value, 2);
        var total = resumo?.Total ?? 0;

        await Contexto.Produtos
            .IgnoreQueryFilters()
            .Where(p => p.Id == idProduto)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.NotaMedia, media)
                    .SetProperty(p => p.TotalAvaliacoes, total),
                cancellationToken);

        DesanexarRastreados<Produto>(p => p.Id == idProduto);
    }
}
