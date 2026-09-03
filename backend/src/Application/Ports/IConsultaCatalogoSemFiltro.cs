using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de acesso ao catalogo IGNORANDO o filtro global de soft delete.
///
/// Por que ela existe: Produto e ProdutoVariacao tem HasQueryFilter(x =&gt; x.Ativo). Isso e
/// correto para a loja, mas torna o painel administrativo cego — o admin nao consegue LISTAR o
/// que desativou nem REATIVAR uma peca, porque a entidade simplesmente nao volta da consulta.
/// O escape hatch do EF (IgnoreQueryFilters) e extensao do EntityFrameworkCore, e a Application
/// nao referencia EF; declarar a necessidade aqui e deixar a Infrastructure implementar mantem
/// a regra de camada intacta.
///
/// Os IQueryable devolvidos ja vem SEM rastreamento e SEM filtro, inclusive nas navegacoes —
/// detalhe que importa: sem ignorar o filtro na navegacao, projetar v.Produto.PrecoBaseCentavos
/// faria a variacao de um produto desativado sumir do resultado em vez de aparecer com o produto.
///
/// Regra dura mantida: nada aqui salva.
/// </summary>
public interface IConsultaCatalogoSemFiltro
{
    /// <summary>Produtos ativos E inativos. Base da tela "produtos desativados" do painel.</summary>
    IQueryable<Produto> Produtos();

    /// <summary>Variacoes ativas E inativas. Sem isto o admin nao enxerga o SKU que desativou.</summary>
    IQueryable<ProdutoVariacao> Variacoes();

    /// <summary>
    /// Produto RASTREADO e sem filtro, pronto para alteracao. Sem navegacoes carregadas de
    /// proposito: Update sobre um grafo carregado marcaria estoque e variacoes como modificados
    /// e reescreveria valores que outra transacao acabou de mudar.
    /// </summary>
    Task<Produto?> ObterProdutoParaEdicaoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Variacao RASTREADA e sem filtro, sem navegacoes. Usada na edicao e na reativacao.</summary>
    Task<ProdutoVariacao?> ObterVariacaoParaEdicaoAsync(int id, CancellationToken cancellationToken = default);
}
