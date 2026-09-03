import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { produtosAdminService } from "@/services/admin/produtosAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";

/**
 * Camada entre page e service para produtos, variacoes e galeria.
 * A page consome nomes de dominio (`produtos`, `variacoes`), nunca `data`.
 */

// ---------------------------------------------------------------- Listagem

export function useProdutosAdmin(filtros = {}) {
    const query = useQuery({
        queryKey: chavesCatalogo.produtos.lista(filtros),
        queryFn: () => produtosAdminService.listar(filtros),
        placeholderData: (anterior) => anterior, // troca de pagina sem piscar
    });

    return {
        produtos: query.data?.itens ?? [],
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

export function useProdutoAdmin(id) {
    const query = useQuery({
        queryKey: chavesCatalogo.produtos.detalhe(id),
        queryFn: () => produtosAdminService.obter(id),
        enabled: !!id,
        retry: false,
    });

    return {
        produto: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
        erro: query.error,
    };
}

// --------------------------------------------------------------- Mutacoes

export function useMutacoesProduto() {
    const queryClient = useQueryClient();

    const invalidarListas = () =>
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.produtos.all });

    const criar = useMutation({
        mutationFn: (payload) => produtosAdminService.criar(payload),
        onSuccess: invalidarListas,
    });

    const atualizar = useMutation({
        mutationFn: ({ id, payload }) => produtosAdminService.atualizar(id, payload),
        onSuccess: invalidarListas,
    });

    const desativar = useMutation({
        mutationFn: (id) => produtosAdminService.desativar(id),
        onSuccess: invalidarListas,
    });

    const ativar = useMutation({
        mutationFn: (id) => produtosAdminService.ativar(id),
        onSuccess: invalidarListas,
    });

    return { criar, atualizar, desativar, ativar };
}

// -------------------------------------------------------------- Variacoes

export function useVariacoesProduto(idProduto, incluirInativas = false) {
    const query = useQuery({
        queryKey: chavesCatalogo.produtos.variacoes(idProduto, incluirInativas),
        queryFn: () => produtosAdminService.variacoes(idProduto, incluirInativas),
        enabled: !!idProduto,
    });

    return {
        variacoes: query.data ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

export function useMutacoesVariacao(idProduto) {
    const queryClient = useQueryClient();

    // A grade mexe em estoque e no total de SKUs do produto: invalida o escopo inteiro.
    const invalidar = () =>
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.produtos.all });

    const gerarGrade = useMutation({
        mutationFn: (payload) => produtosAdminService.gerarGrade(idProduto, payload),
        onSuccess: invalidar,
    });

    const criar = useMutation({
        mutationFn: (payload) => produtosAdminService.criarVariacao(idProduto, payload),
        onSuccess: invalidar,
    });

    const atualizar = useMutation({
        mutationFn: ({ id, payload }) => produtosAdminService.atualizarVariacao(id, payload),
        onSuccess: invalidar,
    });

    const desativar = useMutation({
        mutationFn: (id) => produtosAdminService.desativarVariacao(id),
        onSuccess: invalidar,
    });

    const ativar = useMutation({
        mutationFn: (id) => produtosAdminService.ativarVariacao(id),
        onSuccess: invalidar,
    });

    return { gerarGrade, criar, atualizar, desativar, ativar };
}

// ---------------------------------------------------------------- Galeria

export function useGaleriaProduto(idProduto) {
    const query = useQuery({
        queryKey: chavesCatalogo.produtos.galeria(idProduto),
        queryFn: () => produtosAdminService.galeria(idProduto),
        enabled: !!idProduto,
    });

    return {
        galeria: query.data ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}

export function useMutacoesGaleria(idProduto) {
    const queryClient = useQueryClient();

    const invalidar = () =>
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.produtos.all });

    const vincular = useMutation({
        mutationFn: (payload) => produtosAdminService.vincularMidia(idProduto, payload),
        onSuccess: invalidar,
    });

    const reordenar = useMutation({
        mutationFn: (idsNaOrdem) => produtosAdminService.reordenarGaleria(idProduto, idsNaOrdem),
        onSuccess: invalidar,
    });

    const desvincular = useMutation({
        mutationFn: (idMidia) => produtosAdminService.desvincularMidia(idProduto, idMidia),
        onSuccess: invalidar,
    });

    const alterarCor = useMutation({
        mutationFn: ({ item, idCor }) =>
            produtosAdminService.alterarCorDaFoto(idProduto, item, idCor),
        onSuccess: invalidar,
    });

    return { vincular, reordenar, desvincular, alterarCor };
}

// ------------------------------------------------------------------- Logs

export function useLogsProduto(idProduto, filtros = {}) {
    const query = useQuery({
        queryKey: chavesCatalogo.produtos.logs(idProduto, filtros),
        queryFn: () => produtosAdminService.logs(idProduto, filtros),
        enabled: !!idProduto,
    });

    return {
        logs: query.data?.itens ?? [],
        total: query.data?.total ?? 0,
        totalPaginas: query.data?.totalPaginas ?? 0,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}
