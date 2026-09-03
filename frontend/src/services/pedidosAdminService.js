import api from "@/api/client.js";
import { normalizarPagina } from "@/lib/pagedResult.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

const BASE = "/admin/pedidos";

/**
 * PedidosAdminController — /api/v1/admin/pedidos (policy Expedicao).
 *
 * Todas as rotas identificam o pedido por UUID: o id inteiro nao atravessa a
 * fronteira HTTP. Nao existe DELETE — cancelar e POST /cancelar, porque pedido
 * cancelado continua existindo e auditavel.
 */
export const pedidosAdminService = {
    // GET ?status=&busca=&de=&ate=&page=&pageSize=
    async listar({ status, busca, de, ate, page, pageSize } = {}) {
        const { data } = await api.get(BASE, {
            params: {
                status: status || undefined,
                busca: busca || undefined,
                de: de || undefined,
                ate: ate || undefined,
                page,
                pageSize,
            },
        });
        return normalizarPagina(data, pageSize);
    },

    // GET /{uuid} — inclui a URL da etiqueta, que o detalhe do cliente nunca ve.
    async obter(uuid) {
        try {
            const { data } = await api.get(`${BASE}/${uuid}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    // PATCH /{uuid}/status  { statusNovo, observacao }
    async alterarStatus(uuid, { statusNovo, observacao }) {
        const { data } = await api.patch(`${BASE}/${uuid}/status`, {
            statusNovo,
            observacao: observacao || null,
        });
        return data;
    },

    // POST /{uuid}/cancelar  { motivo }  — devolve estoque e cancela a etiqueta
    async cancelar(uuid, { motivo }) {
        const { data } = await api.post(`${BASE}/${uuid}/cancelar`, { motivo });
        return data;
    },

    // POST /{uuid}/etiqueta — empurra a compra da etiqueta sem esperar o worker
    async gerarEtiqueta(uuid) {
        const { data } = await api.post(`${BASE}/${uuid}/etiqueta`);
        return data;
    },

    /**
     * GET /{uuid}/etiqueta?publico=
     *
     * `publico=true` devolve um link ABERTO (quem tiver a URL abre o PDF). Fica
     * como opt-in explicito, nunca como default da tela.
     * 404 aqui e estado normal: "a etiqueta ainda nao foi gerada".
     */
    async obterUrlEtiqueta(uuid, publico = false) {
        try {
            const { data } = await api.get(`${BASE}/${uuid}/etiqueta`, {
                params: { publico },
            });
            return data?.url ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    // POST /{uuid}/rastreio/sincronizar — puxa o rastreio sob demanda
    async sincronizarRastreio(uuid) {
        const { data } = await api.post(`${BASE}/${uuid}/rastreio/sincronizar`);
        return data;
    },
};

export default pedidosAdminService;
