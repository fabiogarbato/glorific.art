import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { pedidosAdminService } from "@/services/pedidosAdminService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { PAGINA_VAZIA } from "@/lib/pagedResult.js";
import { useToast } from "@/hooks/useToast.js";

/** Fila de pedidos. Paginacao SERVER-SIDE: `filtros` inclui page e pageSize. */
export function usePedidosAdmin(filtros = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.pedidos.lista(filtros),
        queryFn: () => pedidosAdminService.listar(filtros),
        // A pagina anterior fica na tela enquanto a nova carrega — sem isso a
        // tabela pisca em branco a cada clique da paginacao.
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        pedidos: pagina.itens,
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

export function usePedidoAdmin(uuid) {
    const query = useQuery({
        queryKey: chavesOperacao.pedidos.detalhe(uuid),
        queryFn: () => pedidosAdminService.obter(uuid),
        enabled: !!uuid,
        retry: false, // 404 aqui e "pedido nao existe", nao falha transitoria
    });

    return {
        pedido: query.data ?? null,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

/**
 * Acoes de expedicao sobre um pedido.
 *
 * Toda mutation escreve o detalhe no cache com o proprio retorno (a API devolve
 * o pedido inteiro) e so entao invalida a LISTA — assim o detalhe nao pisca e o
 * status novo ja aparece antes do refetch da fila.
 */
export function useAcoesPedido(uuid) {
    const queryClient = useQueryClient();
    const toast = useToast();

    const aplicar = (pedido) => {
        if (pedido) queryClient.setQueryData(chavesOperacao.pedidos.detalhe(uuid), pedido);
        queryClient.invalidateQueries({ queryKey: chavesOperacao.pedidos.all });
        queryClient.invalidateQueries({ queryKey: chavesOperacao.dashboard.all });
        return pedido;
    };

    const alterarStatus = useMutation({
        mutationFn: (dados) => pedidosAdminService.alterarStatus(uuid, dados),
        onSuccess: (pedido) => {
            aplicar(pedido);
            toast.success("Status atualizado.");
        },
    });

    const cancelar = useMutation({
        mutationFn: (dados) => pedidosAdminService.cancelar(uuid, dados),
        onSuccess: (pedido) => {
            aplicar(pedido);
            // O estoque volta para a prateleira no cancelamento.
            queryClient.invalidateQueries({ queryKey: chavesOperacao.estoque.all });
            toast.success("Pedido cancelado e estoque devolvido.");
        },
    });

    const gerarEtiqueta = useMutation({
        mutationFn: () => pedidosAdminService.gerarEtiqueta(uuid),
        onSuccess: (pedido) => {
            aplicar(pedido);
            toast.success("Geração de etiqueta solicitada.");
        },
    });

    const sincronizarRastreio = useMutation({
        mutationFn: () => pedidosAdminService.sincronizarRastreio(uuid),
        onSuccess: (pedido) => {
            aplicar(pedido);
            toast.success("Rastreio sincronizado.");
        },
    });

    /**
     * Link ABERTO do PDF da etiqueta. É mutation e não query de propósito: não é
     * leitura de tela, é um gesto deliberado do operador. Deixar como query faria
     * o link ser buscado sozinho ao abrir o pedido — e um endereço sem senha não
     * se gera por acidente.
     *
     * Devolve `null` quando a etiqueta ainda não existe (404 é estado normal).
     */
    const gerarLinkPublicoEtiqueta = useMutation({
        mutationFn: () => pedidosAdminService.obterUrlEtiqueta(uuid, true),
        onSuccess: (url) => {
            if (!url) toast.warning("A etiqueta ainda não foi gerada para este pedido.");
        },
    });

    return {
        alterarStatus,
        cancelar,
        gerarEtiqueta,
        sincronizarRastreio,
        gerarLinkPublicoEtiqueta,
    };
}

export default usePedidosAdmin;
