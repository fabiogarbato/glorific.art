import api from "@/api/client.js";
import { normalizarPagina } from "@/lib/pagedResult.js";

const BASE = "/admin/estoque";

/**
 * EstoqueAdminController — /api/v1/admin/estoque (policy Expedicao).
 *
 * Nao existe rota "listar todo o estoque paginado" no backend, e isso e
 * deliberado: o relatorio de reposicao (`alerta-minimo`) e uma lista de ACAO,
 * curta por definicao, e o resto do saldo se enxerga pela grade do produto —
 * `ProdutoVariacaoResponseDto` ja carrega quantidade, reservada, disponivel e
 * minima. Por isso as duas leituras de catalogo abaixo moram aqui: sao insumo
 * da TELA de estoque, e nao do CRUD de produto.
 */
export const estoqueAdminService = {
    // GET /alerta-minimo — sem paginacao, de proposito
    async alertaMinimo() {
        const { data } = await api.get(`${BASE}/alerta-minimo`);
        return Array.isArray(data) ? data : [];
    },

    // GET /variacao/{id}
    async obterPorVariacao(idVariacao) {
        const { data } = await api.get(`${BASE}/variacao/${idVariacao}`);
        return data ?? null;
    },

    // PUT /variacao/{id}  { quantidadeMinima, localizacao } — nao mexe em saldo
    async atualizarParametros(idVariacao, { quantidadeMinima, localizacao }) {
        const { data } = await api.put(`${BASE}/variacao/${idVariacao}`, {
            quantidadeMinima: Number(quantidadeMinima) || 0,
            localizacao: localizacao || null,
        });
        return data;
    },

    // POST /entrada  { itens: [{ idVariacao, quantidade }], movimento, observacao }
    async registrarEntrada({ itens, movimento, observacao }) {
        const { data } = await api.post(`${BASE}/entrada`, {
            itens: (itens ?? []).map((item) => ({
                idVariacao: Number(item.idVariacao),
                quantidade: Number(item.quantidade),
            })),
            movimento: movimento || null,
            observacao: observacao || null,
        });
        return Array.isArray(data) ? data : [];
    },

    /**
     * POST /ajuste  { idVariacao, quantidadeContada, movimento, observacao }
     *
     * `quantidadeContada` e o valor FINAL encontrado na prateleira, nunca o
     * delta — quem conta sabe quantas pecas viu, e obrigar a calcular a
     * diferenca de cabeca e a origem classica do ajuste com o sinal trocado.
     */
    async ajustar({ idVariacao, quantidadeContada, movimento, observacao }) {
        const { data } = await api.post(`${BASE}/ajuste`, {
            idVariacao: Number(idVariacao),
            quantidadeContada: Number(quantidadeContada),
            movimento: movimento || null,
            observacao,
        });
        return data;
    },

    // GET /movimentacoes?idVariacao=&idPedido=&movimento=&de=&ate=&page=&pageSize=
    async listarMovimentacoes({
        idVariacao,
        idPedido,
        movimento,
        de,
        ate,
        page,
        pageSize,
    } = {}) {
        const { data } = await api.get(`${BASE}/movimentacoes`, {
            params: {
                idVariacao: idVariacao || undefined,
                idPedido: idPedido || undefined,
                movimento: movimento || undefined,
                de: de || undefined,
                ate: ate || undefined,
                page,
                pageSize,
            },
        });
        return normalizarPagina(data, pageSize);
    },

    // ---------------------------------------------------------------------
    // Leituras de catalogo usadas pela tela de estoque (policy GestaoCatalogo).
    // ---------------------------------------------------------------------

    // GET /api/v1/admin/produtos?ativo=&categoria=&q=&page=&pageSize=
    async listarProdutos({ q, ativo, categoria, page, pageSize } = {}) {
        const { data } = await api.get("/admin/produtos", {
            params: {
                q: q || undefined,
                ativo,
                categoria: categoria || undefined,
                page,
                pageSize,
            },
        });
        return normalizarPagina(data, pageSize);
    },

    // GET /api/v1/admin/produtos/{id}/variacoes — a grade com saldo por SKU
    async listarVariacoes(idProduto) {
        const { data } = await api.get(`/admin/produtos/${idProduto}/variacoes`);
        return Array.isArray(data) ? data : [];
    },
};

export default estoqueAdminService;
