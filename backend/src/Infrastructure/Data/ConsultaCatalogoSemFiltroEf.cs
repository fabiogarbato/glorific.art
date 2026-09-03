using Glorific.Application.Ports;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// Implementacao EF da porta de acesso ao catalogo sem o filtro global de soft delete.
///
/// E o unico lugar da solucao onde IgnoreQueryFilters e chamado em nome do painel administrativo.
/// O detalhe que justifica a existencia deste arquivo: IgnoreQueryFilters vale para a consulta
/// INTEIRA, navegacoes inclusive. Sem ele, projetar v.Produto.PrecoBaseCentavos a partir de uma
/// variacao de produto desativado nao devolveria o preco — devolveria linha nenhuma, porque o
/// filtro do Produto transforma a juncao obrigatoria em juncao vazia.
/// </summary>
public sealed class ConsultaCatalogoSemFiltroEf : IConsultaCatalogoSemFiltro
{
    private readonly GlorificContext _contexto;

    public ConsultaCatalogoSemFiltroEf(GlorificContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public IQueryable<Produto> Produtos() =>
        _contexto.Produtos.AsNoTracking().IgnoreQueryFilters();

    /// <inheritdoc />
    public IQueryable<ProdutoVariacao> Variacoes() =>
        _contexto.ProdutoVariacoes.AsNoTracking().IgnoreQueryFilters();

    /// <inheritdoc />
    public Task<Produto?> ObterProdutoParaEdicaoAsync(int id, CancellationToken cancellationToken = default) =>
        // Rastreado e SEM Include: carregar o grafo faria o ChangeTracker considerar estoque e
        // variacoes como parte da edicao, e um SaveChanges reescreveria valores que outra
        // transacao acabou de alterar.
        _contexto.Produtos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<ProdutoVariacao?> ObterVariacaoParaEdicaoAsync(int id, CancellationToken cancellationToken = default) =>
        _contexto.ProdutoVariacoes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
}
