using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class ProdutoVariacaoRepository : BaseRepository<ProdutoVariacao>, IProdutoVariacaoRepository
{
    public ProdutoVariacaoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<ProdutoVariacao?> ObterPorSkuAsync(string sku, CancellationToken cancellationToken = default) =>
        Query()
            .Include(v => v.Tamanho)
            .Include(v => v.Cor)
            .Include(v => v.Estoque)
            .FirstOrDefaultAsync(v => v.Sku == sku, cancellationToken);

    /// <summary>
    /// Grade do produto na ordem do seletor: tamanho por Ordem (P, M, G, GG), nunca alfabetica.
    /// </summary>
    public async Task<IReadOnlyList<ProdutoVariacao>> ObterPorProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(v => v.IdProduto == idProduto)
            .Include(v => v.Tamanho)
            .Include(v => v.Cor)
            .Include(v => v.Estoque)
            .OrderBy(v => v.Cor.Ordem)
            .ThenBy(v => v.Tamanho.Ordem)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Uma consulta so para o carrinho inteiro: produto, tamanho, cor e estoque carregados.
    ///
    /// O checkout monta o snapshot de cada item do pedido a partir daqui — nome, sku, tamanho,
    /// cor, peso e preco — e o snapshot e o que faz o recibo antigo continuar correto quando o
    /// catalogo muda. Sem o carregamento em bloco isso vira quatro consultas por item.
    ///
    /// O filtro de soft delete continua ligado de proposito: variacao desativada nao entra no
    /// checkout, e o id que "some" da resposta e o sinal para o caso de uso recusar a compra.
    /// </summary>
    public async Task<IReadOnlyList<ProdutoVariacao>> ObterParaCheckoutAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
            return [];

        var distintos = ids.Distinct().ToArray();

        return await Query()
            .Where(v => distintos.Contains(v.Id))
            .Include(v => v.Produto)
            .Include(v => v.Tamanho)
            .Include(v => v.Cor)
            .Include(v => v.Estoque)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Protege o unico (produto, tamanho, cor) antes de o banco devolver violacao crua na tela.
    /// IgnoreQueryFilters: variacao desativada ainda ocupa a combinacao no indice unico.
    /// </summary>
    public Task<bool> CombinacaoEmUsoAsync(
        int idProduto,
        int idTamanho,
        int idCor,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query()
            .IgnoreQueryFilters()
            .AnyAsync(
                v => v.IdProduto == idProduto
                     && v.IdTamanho == idTamanho
                     && v.IdCor == idCor
                     && (idIgnorar == null || v.Id != idIgnorar),
                cancellationToken);

    public Task<bool> SkuEmUsoAsync(
        string sku,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query()
            .IgnoreQueryFilters()
            .AnyAsync(
                v => v.Sku == sku && (idIgnorar == null || v.Id != idIgnorar),
                cancellationToken);
}
