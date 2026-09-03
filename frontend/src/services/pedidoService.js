/**
 * Pedidos do proprio cliente (`/api/v1/pedidos`).
 *
 * Todo pedido e identificado por `uuid`, nunca pelo id inteiro — id sequencial em
 * URL e convite a enumeracao. Pedido de outra pessoa responde 404 no backend.
 *
 * Tudo que aparece no detalhe e snapshot congelado na compra: renomear o produto
 * no catalogo nao pode reescrever recibo antigo.
 */
import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

/** Envelope de paginacao do backend: { items, page, pageSize, total, totalPages }. */
const PAGINA_VAZIA = Object.freeze({
    items: [],
    page: 1,
    pageSize: 0,
    total: 0,
    totalPages: 0,
});

export const pedidoService = {
    // GET /api/v1/pedidos?page=&pageSize=  — do mais recente para o mais antigo.
    async listarMeus({ page = 1, pageSize = 10 } = {}) {
        const { data } = await api.get("/pedidos", { params: { page, pageSize } });

        return {
            ...PAGINA_VAZIA,
            ...data,
            items: Array.isArray(data?.items) ? data.items : [],
            total: Number(data?.total) || 0,
            totalPages: Number(data?.totalPages) || 0,
        };
    },

    // GET /api/v1/pedidos/{uuid} — o recibo completo. 404 = nao e seu / nao existe.
    async obter(uuid) {
        try {
            const { data } = await api.get(`/pedidos/${uuid}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    /**
     * GET /api/v1/pedidos/{uuid}/rastreio — historico ja gravado em envios_eventos.
     * O backend nao consulta a transportadora a cada request (isso e do worker),
     * entao pedido sem postagem devolve lista vazia, e isso e normal.
     */
    async rastreio(uuid) {
        try {
            const { data } = await api.get(`/pedidos/${uuid}/rastreio`);
            if (!data) return null;
            return { ...data, eventos: Array.isArray(data.eventos) ? data.eventos : [] };
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },
};

export default pedidoService;
