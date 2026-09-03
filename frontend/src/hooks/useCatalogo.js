import { useQuery } from "@tanstack/react-query";
import { catalogoService } from "@/services/catalogoService.js";
import { queryKeys } from "@/lib/queryKeys.js";
import {
    filtrosParaApi,
    filtrosParaApiFacetas,
    TAMANHO_PAGINA_CATALOGO,
} from "@/lib/vitrine.js";

/**
 * Camada obrigatoria entre page e service. A page recebe nomes de dominio
 * (`produtos`, `facetas`, `produto`) e nunca toca em `data`.
 *
 * Catalogo e conteudo editorial: muda quando o admin publica, nao a cada
 * segundo. Por isso `staleTime` generoso — evita refetch a cada volta do
 * navegador sem deixar a vitrine velha por horas.
 */
const CINCO_MINUTOS = 1000 * 60 * 5;
const MEIA_HORA = 1000 * 60 * 30;

/**
 * Vitrine paginada.
 * `placeholderData` mantem a pagina anterior na tela durante a troca de pagina
 * ou de filtro: sem isso o grid pisca vazio a cada clique.
 */
export function useCatalogo(filtros, { pageSize = TAMANHO_PAGINA_CATALOGO } = {}) {
    const params = filtrosParaApi(filtros, { pageSize });

    const query = useQuery({
        queryKey: queryKeys.catalogo.lista(params),
        queryFn: () => catalogoService.listar(params),
        staleTime: CINCO_MINUTOS,
        placeholderData: (anterior) => anterior,
    });

    return {
        produtos: query.data?.itens ?? [],
        total: query.data?.total ?? 0,
        pagina: query.data?.pagina ?? filtros?.pagina ?? 1,
        tamanhoPagina: query.data?.tamanhoPagina ?? pageSize,
        totalPaginas: query.data?.totalPaginas ?? 0,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/** Contagens dos filtros, no mesmo recorte da listagem (menos a paginacao). */
export function useFacetasCatalogo(filtros) {
    const params = filtrosParaApiFacetas(filtros);

    const query = useQuery({
        queryKey: queryKeys.catalogo.facetas(params),
        queryFn: () => catalogoService.facetas(params),
        staleTime: CINCO_MINUTOS,
        placeholderData: (anterior) => anterior,
    });

    return {
        facetas: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

/** Detalhe por slug. `produto === null` com `isLoading false` significa 404. */
export function useProduto(slug) {
    const query = useQuery({
        queryKey: queryKeys.catalogo.produto(slug),
        queryFn: () => catalogoService.obterProduto(slug),
        enabled: !!slug,
        retry: false, // 404 aqui e "nao existe", nao falha transitoria
        staleTime: CINCO_MINUTOS,
    });

    return {
        produto: query.data ?? null,
        naoEncontrado: !query.isLoading && !query.isError && query.data === null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

export function useRelacionados(slug, limite = 4) {
    const query = useQuery({
        queryKey: queryKeys.catalogo.relacionados(slug, limite),
        queryFn: () => catalogoService.relacionados(slug, limite),
        enabled: !!slug,
        staleTime: CINCO_MINUTOS,
    });

    return {
        relacionados: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

/** Vitrine da home: `destaque=true` ordenado por novidade. */
export function useDestaques(limite = 8) {
    const query = useQuery({
        queryKey: queryKeys.catalogo.destaques(limite),
        queryFn: () => catalogoService.destaques(limite),
        staleTime: CINCO_MINUTOS,
    });

    return {
        destaques: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useCategorias() {
    const query = useQuery({
        queryKey: queryKeys.categorias.lista(),
        queryFn: catalogoService.categorias,
        staleTime: MEIA_HORA,
    });

    return {
        categorias: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useCategoria(slug) {
    const query = useQuery({
        queryKey: queryKeys.categorias.detalhe(slug),
        queryFn: () => catalogoService.obterCategoria(slug),
        enabled: !!slug,
        retry: false,
        staleTime: MEIA_HORA,
    });

    return {
        categoria: query.data ?? null,
        naoEncontrada: !query.isLoading && !query.isError && query.data === null,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useColecoes() {
    const query = useQuery({
        queryKey: queryKeys.colecoes.lista(),
        queryFn: catalogoService.colecoes,
        staleTime: MEIA_HORA,
    });

    return {
        colecoes: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useColecao(slug) {
    const query = useQuery({
        queryKey: queryKeys.colecoes.detalhe(slug),
        queryFn: () => catalogoService.obterColecao(slug),
        enabled: !!slug,
        retry: false,
        staleTime: MEIA_HORA,
    });

    return {
        colecao: query.data ?? null,
        naoEncontrada: !query.isLoading && !query.isError && query.data === null,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

/** Grade de tamanhos ativa, ja na ordem de exibicao (nunca alfabetica). */
export function useTamanhos(grade = null) {
    const query = useQuery({
        queryKey: queryKeys.catalogo.tamanhos(grade),
        queryFn: () => catalogoService.tamanhos(grade),
        staleTime: MEIA_HORA,
    });

    return {
        tamanhos: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export function useCores() {
    const query = useQuery({
        queryKey: queryKeys.catalogo.cores(),
        queryFn: catalogoService.cores,
        staleTime: MEIA_HORA,
    });

    return {
        cores: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

export default useCatalogo;
