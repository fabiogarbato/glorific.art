/**
 * Traducao do `PagedResult<T>` do backend para o vocabulario do front.
 *
 * O envelope real da API e:
 *   { items, page, pageSize, total, totalPages, temProximaPagina, temPaginaAnterior }
 *
 * Toda listagem administrativa deste modulo passa por aqui — a page nunca le
 * `data.items` na mao.
 */

/** Teto do `PageRequest` do backend (`PageRequest.TamanhoMaximo`). */
export const TAMANHO_PAGINA_MAXIMO = 100;

/** Default do backend quando `pageSize` nao vem (`PageRequest.TamanhoPadrao`). */
export const TAMANHO_PAGINA_PADRAO = 20;

/** Mantem `pageSize` dentro do que o backend aceita — pedir 500 volta 100 e a conta de paginas erra. */
export function limitarTamanhoPagina(tamanho) {
    const n = Number(tamanho);
    if (!Number.isFinite(n) || n < 1) return TAMANHO_PAGINA_PADRAO;
    return Math.min(Math.trunc(n), TAMANHO_PAGINA_MAXIMO);
}

export function normalizarPagina(data) {
    const itens = Array.isArray(data?.items) ? data.items : [];
    const tamanhoPagina = Number(data?.pageSize) || TAMANHO_PAGINA_PADRAO;
    const total = Number(data?.total) || 0;

    return {
        itens,
        pagina: Number(data?.page) || 1,
        tamanhoPagina,
        total,
        // `totalPages` e derivado no backend; recalcular aqui protege de resposta truncada.
        totalPaginas: Number(data?.totalPages) || Math.ceil(total / tamanhoPagina) || 0,
    };
}

/** Monta os query params de paginacao ja normalizados. */
export function paramsPaginacao(pagina, tamanhoPagina) {
    return {
        page: Math.max(1, Number(pagina) || 1),
        pageSize: limitarTamanhoPagina(tamanhoPagina),
    };
}

/** Remove chaves vazias para nao mandar `?q=&categoria=` para a API. */
export function limparParams(params = {}) {
    return Object.fromEntries(
        Object.entries(params).filter(
            ([, valor]) => valor !== undefined && valor !== null && valor !== "",
        ),
    );
}
