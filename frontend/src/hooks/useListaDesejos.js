import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { listaDesejoService } from "@/services/listaDesejoService.js";
import { queryKeys } from "@/lib/queryKeys.js";
import { useAuth } from "@/hooks/useAuth.js";

/**
 * Lista de desejos completa (com capa, preco e disponibilidade).
 * So consulta quando ha sessao — a rota exige token e um 401 aqui derrubaria o
 * visitante para o login sem ele ter pedido nada.
 */
export function useListaDesejos() {
    const { estaAutenticado } = useAuth();

    const query = useQuery({
        queryKey: queryKeys.listaDesejos.lista(),
        queryFn: listaDesejoService.listar,
        enabled: estaAutenticado,
        staleTime: 1000 * 30,
    });

    return {
        itens: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/**
 * Apenas os ids favoritados. E o que pinta o coracao em todos os cards da
 * vitrine sem uma requisicao por card.
 */
export function useIdsListaDesejos() {
    const { estaAutenticado } = useAuth();

    const query = useQuery({
        queryKey: queryKeys.listaDesejos.ids(),
        queryFn: listaDesejoService.ids,
        enabled: estaAutenticado,
        staleTime: 1000 * 60,
    });

    const ids = query.data ?? [];

    return {
        ids,
        ehFavorito: (idProduto) => ids.includes(Number(idProduto)),
        isLoading: query.isLoading,
    };
}

/** Toda escrita derruba as duas leituras: a lista e os ids da vitrine. */
function useMutacaoListaDesejos(mutationFn) {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.listaDesejos.all });
        },
    });
}

/** Toggle do coracao. Devolve `true` quando a peca passou a fazer parte da lista. */
export function useAlternarListaDesejos() {
    return useMutacaoListaDesejos(listaDesejoService.alternar);
}

export function useAdicionarListaDesejos() {
    return useMutacaoListaDesejos(listaDesejoService.adicionar);
}

/** A remocao e por ID DE PRODUTO: a chave de negocio da lista e usuario + produto. */
export function useRemoverListaDesejos() {
    return useMutacaoListaDesejos(listaDesejoService.remover);
}
