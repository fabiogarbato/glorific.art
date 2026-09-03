import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { cuponsService } from "@/services/cuponsService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { PAGINA_VAZIA } from "@/lib/pagedResult.js";
import { useToast } from "@/hooks/useToast.js";

export function useCupons(filtros = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.cupons.lista(filtros),
        queryFn: () => cuponsService.listar(filtros),
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        cupons: pagina.itens,
        total: pagina.total,
        pagina: pagina.pagina,
        totalPaginas: pagina.totalPaginas,
        tamanhoPagina: pagina.tamanhoPagina,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/** Ledger de usos de um cupom — o custo real da campanha. */
export function useUsosDoCupom(id, { page = 1, pageSize = 10 } = {}, { habilitado = true } = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.cupons.usos(id, page),
        queryFn: () => cuponsService.listarUsos(id, { page, pageSize }),
        enabled: !!id && habilitado,
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        usos: pagina.itens,
        total: pagina.total,
        totalPaginas: pagina.totalPaginas,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useAcoesCupom() {
    const queryClient = useQueryClient();
    const toast = useToast();

    const invalidar = () =>
        queryClient.invalidateQueries({ queryKey: chavesOperacao.cupons.all });

    const criar = useMutation({
        mutationFn: (cupom) => cuponsService.criar(cupom),
        onSuccess: () => {
            invalidar();
            toast.success("Cupom criado.");
        },
    });

    const atualizar = useMutation({
        mutationFn: ({ id, ...cupom }) => cuponsService.atualizar(id, cupom),
        onSuccess: () => {
            invalidar();
            toast.success("Cupom salvo.");
        },
    });

    const remover = useMutation({
        mutationFn: (id) => cuponsService.remover(id),
        onSuccess: () => {
            invalidar();
            toast.success("Cupom excluído.");
        },
    });

    return { criar, atualizar, remover };
}

export default useCupons;
