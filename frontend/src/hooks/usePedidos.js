import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { pedidoService } from "@/services/pedidoService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/** Quantos pedidos por pagina em "meus pedidos" — lista com foto, cabe menos. */
export const PEDIDOS_POR_PAGINA = 10;

/**
 * Meus pedidos, paginado, do mais recente para o mais antigo.
 * `keepPreviousData` evita o esqueleto piscando a cada troca de pagina.
 */
export function useMeusPedidos(pagina = 1, { habilitado = true } = {}) {
    const query = useQuery({
        queryKey: queryKeys.pedidos.meus(pagina),
        queryFn: () => pedidoService.listarMeus({ page: pagina, pageSize: PEDIDOS_POR_PAGINA }),
        enabled: habilitado,
        placeholderData: keepPreviousData,
    });

    return {
        pedidos: query.data?.items ?? [],
        pagina: query.data?.page ?? pagina,
        totalPaginas: query.data?.totalPages ?? 0,
        total: query.data?.total ?? 0,
        itensPorPagina: query.data?.pageSize ?? PEDIDOS_POR_PAGINA,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/**
 * Detalhe do pedido (o recibo).
 * `retry: false` porque 404 aqui e "nao e seu" — repetir nao muda a resposta.
 */
export function usePedido(uuid) {
    const query = useQuery({
        queryKey: queryKeys.pedidos.detalhe(uuid),
        queryFn: () => pedidoService.obter(uuid),
        enabled: !!uuid,
        retry: false,
    });

    return {
        pedido: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/**
 * Timeline de rastreio. Le o historico ja gravado pelo worker — pedido que ainda
 * nao foi postado devolve lista vazia, e isso e estado normal, nao erro.
 */
export function useRastreio(uuid, { habilitado = true } = {}) {
    const query = useQuery({
        queryKey: queryKeys.pedidos.rastreio(uuid),
        queryFn: () => pedidoService.rastreio(uuid),
        enabled: !!uuid && habilitado,
        retry: false,
    });

    return {
        rastreio: query.data ?? null,
        eventos: query.data?.eventos ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}
