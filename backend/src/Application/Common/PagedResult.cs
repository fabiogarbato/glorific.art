namespace Glorific.Application.Common;

/// <summary>
/// Pagina de resultado de uma consulta. Existe porque o repo de referencia nao tinha paginacao
/// em lugar nenhum: GET /api/Produto carregava a tabela inteira, rastreada, com tres niveis de
/// Include. Toda listagem administrativa desta camada devolve PagedResult, nunca IEnumerable.
///
/// Total e a contagem no banco (COUNT antes do Skip/Take), nao Items.Count.
/// TotalPages e derivado — nunca aceite esse numero vindo de fora.
/// </summary>
public sealed record PagedResult<T>
{
    /// <summary>Itens da pagina atual, ja materializados.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Pagina atual, base 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamanho da pagina efetivamente aplicado (ja normalizado por PageRequest).</summary>
    public int PageSize { get; init; } = PageRequest.TamanhoPadrao;

    /// <summary>Total de registros que satisfazem o filtro, ignorando a paginacao.</summary>
    public int Total { get; init; }

    /// <summary>Derivado de Total e PageSize. Zero quando nao ha registros.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool TemProximaPagina => Page < TotalPages;

    public bool TemPaginaAnterior => Page > 1 && TotalPages > 0;

    public static PagedResult<T> Criar(IReadOnlyList<T> itens, int pagina, int tamanhoPagina, int total) =>
        new()
        {
            Items = itens,
            Page = pagina < 1 ? 1 : pagina,
            PageSize = tamanhoPagina < 1 ? PageRequest.TamanhoPadrao : tamanhoPagina,
            Total = total < 0 ? 0 : total
        };

    public static PagedResult<T> Criar(IReadOnlyList<T> itens, PageRequest requisicao, int total) =>
        Criar(itens, requisicao.Page, requisicao.PageSize, total);

    public static PagedResult<T> Vazio(int pagina = 1, int tamanhoPagina = PageRequest.TamanhoPadrao) =>
        Criar([], pagina, tamanhoPagina, 0);

    /// <summary>Projeta os itens mantendo os metadados de paginacao intactos.</summary>
    public PagedResult<TDestino> Mapear<TDestino>(Func<T, TDestino> projecao) =>
        PagedResult<TDestino>.Criar([.. Items.Select(projecao)], Page, PageSize, Total);
}
