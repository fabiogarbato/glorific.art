import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { estoqueAdminService } from "@/services/estoqueAdminService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { PAGINA_VAZIA } from "@/lib/pagedResult.js";
import { useToast } from "@/hooks/useToast.js";

/** Relatorio de reposicao: disponivel abaixo do minimo. Sem paginacao. */
export function useAlertaMinimo({ habilitado = true } = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.estoque.alertaMinimo(),
        queryFn: estoqueAdminService.alertaMinimo,
        enabled: habilitado,
    });

    return {
        criticos: query.data ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/** Produtos para escolher a grade cujo saldo se quer enxergar. */
export function useProdutosParaEstoque(filtros = {}, { habilitado = true } = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.estoque.produtos(filtros),
        queryFn: () => estoqueAdminService.listarProdutos(filtros),
        enabled: habilitado,
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        produtos: pagina.itens,
        total: pagina.total,
        pagina: pagina.pagina,
        totalPaginas: pagina.totalPaginas,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

/** Grade do produto — ja traz fisico, reservado, disponivel e minimo por SKU. */
export function useVariacoesDoProduto(idProduto) {
    const query = useQuery({
        queryKey: chavesOperacao.estoque.variacoesDoProduto(idProduto),
        queryFn: () => estoqueAdminService.listarVariacoes(idProduto),
        enabled: !!idProduto,
    });

    return {
        variacoes: query.data ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

/** Extrato do ledger. Append-only e paginado no servidor. */
export function useMovimentacoes(filtros = {}, { habilitado = true } = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.estoque.movimentacoes(filtros),
        queryFn: () => estoqueAdminService.listarMovimentacoes(filtros),
        enabled: habilitado,
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        movimentacoes: pagina.itens,
        total: pagina.total,
        pagina: pagina.pagina,
        totalPaginas: pagina.totalPaginas,
        tamanhoPagina: pagina.tamanhoPagina,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

/**
 * Escrita de estoque. Qualquer sucesso derruba TODO o escopo de estoque: o
 * ajuste muda saldo, alerta e ledger de uma vez, e invalidar so a lista visivel
 * deixaria o alerta mentindo na aba do lado.
 */
export function useAcoesEstoque() {
    const queryClient = useQueryClient();
    const toast = useToast();

    const invalidar = () => {
        queryClient.invalidateQueries({ queryKey: chavesOperacao.estoque.all });
        queryClient.invalidateQueries({ queryKey: chavesOperacao.dashboard.all });
    };

    const ajustar = useMutation({
        mutationFn: (dados) => estoqueAdminService.ajustar(dados),
        onSuccess: () => {
            invalidar();
            toast.success("Ajuste registrado no extrato.");
        },
    });

    const registrarEntrada = useMutation({
        mutationFn: (dados) => estoqueAdminService.registrarEntrada(dados),
        onSuccess: () => {
            invalidar();
            toast.success("Entrada lançada.");
        },
    });

    const atualizarParametros = useMutation({
        mutationFn: ({ idVariacao, ...dados }) =>
            estoqueAdminService.atualizarParametros(idVariacao, dados),
        onSuccess: () => {
            invalidar();
            toast.success("Parâmetros do SKU salvos.");
        },
    });

    return { ajustar, registrarEntrada, atualizarParametros };
}

export default useAlertaMinimo;
