import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { avaliacoesService } from "@/services/avaliacoesService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/** Cinco por vez: a lista de avaliacoes nao pode empurrar a compra para baixo. */
export const AVALIACOES_POR_PAGINA = 5;

export function useAvaliacoes(idProduto, pagina = 1) {
    const query = useQuery({
        queryKey: queryKeys.avaliacoes.doProduto(idProduto, pagina),
        queryFn: () =>
            avaliacoesService.listarDoProduto(idProduto, {
                pagina,
                tamanhoPagina: AVALIACOES_POR_PAGINA,
            }),
        enabled: !!idProduto,
        placeholderData: (anterior) => anterior,
    });

    return {
        avaliacoes: query.data?.itens ?? [],
        total: query.data?.total ?? 0,
        pagina: query.data?.pagina ?? pagina,
        totalPaginas: query.data?.totalPaginas ?? 0,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useResumoAvaliacoes(idProduto) {
    const query = useQuery({
        queryKey: queryKeys.avaliacoes.resumo(idProduto),
        queryFn: () => avaliacoesService.resumoDoProduto(idProduto),
        enabled: !!idProduto,
    });

    return {
        resumo: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

/**
 * Envio de avaliacao.
 *
 * O 201 significa "recebida" — a avaliacao nasce PENDENTE e so aparece depois
 * da moderacao. Invalidamos mesmo assim: o resumo pode mudar de outras fontes e
 * a lista precisa refletir o estado real do servidor, nao um otimismo local.
 */
export function useCriarAvaliacao(idProduto) {
    const queryClient = useQueryClient();

    const mutation = useMutation({
        mutationFn: (dados) => avaliacoesService.criar({ ...dados, idProduto }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.avaliacoes.all });
        },
    });

    return {
        enviar: mutation.mutateAsync,
        enviando: mutation.isPending,
        enviada: mutation.isSuccess,
        erro: mutation.error,
        reiniciar: mutation.reset,
    };
}

export default useAvaliacoes;
