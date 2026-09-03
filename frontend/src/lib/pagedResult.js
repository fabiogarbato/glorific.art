/**
 * Tradutor do `PagedResult<T>` do backend para o vocabulario do front.
 *
 * O backend manda `{ items, page, pageSize, total, totalPages, temProximaPagina,
 * temPaginaAnterior }` (camelCase do JsonSerializerDefaults.Web). Um unico ponto
 * de traducao evita `data?.items ?? data?.itens ?? []` espalhado por dez hooks.
 *
 * `totalPaginas` e recalculado quando o backend nao manda: o proprio DTO diz que
 * esse numero e derivado e nunca deve ser aceito de fora sem conferencia.
 */
export function normalizarPagina(data, tamanhoPadrao = 20) {
    const itens = data?.items ?? data?.itens ?? [];
    const total = Number(data?.total ?? itens.length) || 0;
    const tamanhoPagina = Number(data?.pageSize ?? data?.tamanhoPagina ?? tamanhoPadrao) || tamanhoPadrao;

    return {
        itens,
        pagina: Number(data?.page ?? data?.pagina ?? 1) || 1,
        tamanhoPagina,
        total,
        totalPaginas:
            Number(data?.totalPages ?? data?.totalPaginas) ||
            (tamanhoPagina > 0 ? Math.ceil(total / tamanhoPagina) : 0),
    };
}

/** Pagina vazia — valor neutro para `useQuery` sem dado ainda. */
export const PAGINA_VAZIA = {
    itens: [],
    pagina: 1,
    tamanhoPagina: 20,
    total: 0,
    totalPaginas: 0,
};

export default normalizarPagina;
