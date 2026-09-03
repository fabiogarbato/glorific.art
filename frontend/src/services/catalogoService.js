import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

/**
 * Vitrine publica — CatalogoController (`/api/v1`).
 *
 * Rotas exatas do backend (nao ha prefixo `/catalogo` em produto, categoria e
 * colecao: o blueprint trata `/produtos/{slug}`, `/categorias` e `/colecoes`
 * como enderecos de primeira classe porque sao eles que vao para o sitemap):
 *
 *   GET /api/v1/catalogo                     -> PagedResult<ProdutoCardDto>
 *   GET /api/v1/catalogo/facetas             -> FacetasCatalogoDto
 *   GET /api/v1/produtos/{slug}              -> ProdutoDetalheDto
 *   GET /api/v1/produtos/{slug}/relacionados -> ProdutoCardDto[]
 *   GET /api/v1/categorias | /categorias/{slug}
 *   GET /api/v1/colecoes   | /colecoes/{slug}
 *   GET /api/v1/tamanhos?grade= | /cores
 *
 * Convencoes desta camada:
 *  - dinheiro chega e sai em CENTAVOS (inteiro), sempre;
 *  - 404 e estado de dominio ("nao existe"), entao vira `null` em vez de excecao;
 *  - o PagedResult do backend (`items/page/pageSize/total/totalPages`) e
 *    traduzido UMA vez aqui para o vocabulario do front.
 */

/** PagedResult<T> do backend -> pagina em portugues, com defaults seguros. */
function normalizarPagina(data, tamanhoPadrao = 0) {
    const itens = Array.isArray(data?.items) ? data.items : [];
    const tamanhoPagina = data?.pageSize ?? tamanhoPadrao;
    const total = data?.total ?? itens.length;

    return {
        itens,
        pagina: data?.page ?? 1,
        tamanhoPagina,
        total,
        totalPaginas:
            data?.totalPages ?? (tamanhoPagina > 0 ? Math.ceil(total / tamanhoPagina) : 0),
    };
}

export const catalogoService = {
    /**
     * Listagem da vitrine. `params` ja vem pronto de `filtrosParaApi()` —
     * o service nao adivinha filtro nem inventa nome de parametro.
     */
    async listar(params = {}) {
        const { data } = await api.get("/catalogo", { params });
        return normalizarPagina(data, params.pageSize ?? 0);
    },

    /** Contagens dos filtros. Sem elas o cliente clica em "GG" e recebe zero. */
    async facetas(params = {}) {
        const { data } = await api.get("/catalogo/facetas", { params });
        return {
            categorias: data?.categorias ?? [],
            colecoes: data?.colecoes ?? [],
            tamanhos: data?.tamanhos ?? [],
            cores: data?.cores ?? [],
            precoMinCentavos: data?.precoMinCentavos ?? 0,
            precoMaxCentavos: data?.precoMaxCentavos ?? 0,
            totalProdutos: data?.totalProdutos ?? 0,
        };
    },

    /** Pagina de produto. 404 = slug inexistente ou peca despublicada. */
    async obterProduto(slug) {
        try {
            const { data } = await api.get(`/produtos/${encodeURIComponent(slug)}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    async relacionados(slug, limite = 8) {
        try {
            const { data } = await api.get(
                `/produtos/${encodeURIComponent(slug)}/relacionados`,
                { params: { limite } },
            );
            return Array.isArray(data) ? data : [];
        } catch (err) {
            if (ehNaoEncontrado(err)) return [];
            throw err;
        }
    },

    /** Arvore de um nivel (pai + filhas habilitadas): o menu do site. */
    async categorias() {
        const { data } = await api.get("/categorias");
        return Array.isArray(data) ? data : [];
    },

    async obterCategoria(slug) {
        try {
            const { data } = await api.get(`/categorias/${encodeURIComponent(slug)}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    /** Somente as vigentes — e o que faz o drop agendado entrar no ar sozinho. */
    async colecoes() {
        const { data } = await api.get("/colecoes");
        return Array.isArray(data) ? data : [];
    },

    async obterColecao(slug) {
        try {
            const { data } = await api.get(`/colecoes/${encodeURIComponent(slug)}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    /** Tamanhos ativos na ordem da grade — nunca alfabetica. */
    async tamanhos(grade) {
        const { data } = await api.get("/tamanhos", {
            params: grade ? { grade } : undefined,
        });
        return Array.isArray(data) ? data : [];
    },

    async cores() {
        const { data } = await api.get("/cores");
        return Array.isArray(data) ? data : [];
    },

    /**
     * Destaques da home. Nao existe endpoint proprio: e a vitrine com
     * `destaque=true`, que e exatamente o que o filtro do backend expoe.
     */
    async destaques(limite = 8) {
        const { itens } = await catalogoService.listar({
            destaque: true,
            pageSize: limite,
            sort: "Novidade",
        });
        return itens;
    },
};

export default catalogoService;
