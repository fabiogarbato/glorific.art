import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { avaliacoesAdminService } from "@/services/avaliacoesAdminService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { PAGINA_VAZIA } from "@/lib/pagedResult.js";
import { useToast } from "@/hooks/useToast.js";

/** Fila de moderacao. Sem `status`, o backend devolve as pendentes mais antigas. */
export function useAvaliacoesAdmin(filtros = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.avaliacoes.lista(filtros),
        queryFn: () => avaliacoesAdminService.listar(filtros),
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        avaliacoes: pagina.itens,
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

/**
 * Moderar recalcula NotaMedia e TotalAvaliacoes do produto no backend, entao a
 * invalidacao alcanca tambem o dashboard (contador de pendentes) — e nao so a
 * fila que esta na tela.
 */
export function useAcoesAvaliacao() {
    const queryClient = useQueryClient();
    const toast = useToast();

    const invalidar = () => {
        queryClient.invalidateQueries({ queryKey: chavesOperacao.avaliacoes.all });
        queryClient.invalidateQueries({ queryKey: chavesOperacao.dashboard.all });
    };

    const aprovar = useMutation({
        mutationFn: (id) => avaliacoesAdminService.aprovar(id),
        onSuccess: () => {
            invalidar();
            toast.success("Avaliação publicada.");
        },
    });

    const rejeitar = useMutation({
        mutationFn: ({ id, motivo }) => avaliacoesAdminService.rejeitar(id, motivo),
        onSuccess: () => {
            invalidar();
            toast.success("Avaliação rejeitada.");
        },
    });

    return { aprovar, rejeitar };
}

export default useAvaliacoesAdmin;
