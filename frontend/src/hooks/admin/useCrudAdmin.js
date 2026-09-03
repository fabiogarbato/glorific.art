import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

/**
 * Par listagem + mutacoes para os recursos que herdam o `GenericController`.
 *
 * Recebe `servico` (objeto do service, ja com base propria) e `chaves` (o no
 * correspondente de `queryKeysAdminCatalogo`). Toda invalidacao e por PREFIXO
 * (`chaves.all`), entao salvar uma cor derruba a lista paginada e a `ativas`
 * de uma vez — nunca sobra lista velha na tela.
 */
export function useCrudAdmin(servico, chaves) {
    const queryClient = useQueryClient();

    const invalidar = () => queryClient.invalidateQueries({ queryKey: chaves.all });

    const criar = useMutation({
        mutationFn: (payload) => servico.criar(payload),
        onSuccess: invalidar,
    });

    const atualizar = useMutation({
        mutationFn: ({ id, payload }) => servico.atualizar(id, payload),
        onSuccess: invalidar,
    });

    const remover = useMutation({
        mutationFn: (id) => servico.remover(id),
        onSuccess: invalidar,
    });

    return { criar, atualizar, remover, invalidar };
}

/** Listagem paginada padrao do CRUD generico. */
export function useListaCrudAdmin(servico, chaves, filtros = {}) {
    const query = useQuery({
        queryKey: chaves.lista(filtros),
        queryFn: () => servico.listar(filtros),
        placeholderData: (anterior) => anterior,
    });

    return {
        itens: query.data?.itens ?? [],
        total: query.data?.total ?? 0,
        pagina: query.data?.pagina ?? 1,
        tamanhoPagina: query.data?.tamanhoPagina ?? 0,
        totalPaginas: query.data?.totalPaginas ?? 0,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        erro: query.error,
        refetch: query.refetch,
    };
}
