import api from "@/api/client.js";
import { normalizarPagina } from "@/lib/pagedResult.js";

const BASE = "/admin/avaliacoes";

/**
 * AvaliacoesAdminController — /api/v1/admin/avaliacoes (policy GestaoCatalogo).
 *
 * Sem `status` na query, o backend devolve as PENDENTES, da mais antiga para a
 * mais nova. `StatusAvaliacao` viaja como numero (1 pendente, 2 aprovada,
 * 3 rejeitada) porque o Program.cs nao registra JsonStringEnumConverter.
 */
export const avaliacoesAdminService = {
    async listar({ status, page, pageSize } = {}) {
        const { data } = await api.get(BASE, {
            params: {
                status: status ?? undefined,
                page,
                pageSize,
            },
        });
        return normalizarPagina(data, pageSize);
    },

    /** POST /{id}/aprovar — publica e recalcula a nota media do produto. */
    async aprovar(id) {
        const { data } = await api.post(`${BASE}/${id}/aprovar`);
        return data;
    },

    /**
     * POST /{id}/rejeitar  { motivo }
     * Motivo e obrigatorio (3 a 400 caracteres): rejeicao sem motivo registrado
     * impede responder ao cliente que perguntar por que a review sumiu.
     */
    async rejeitar(id, motivo) {
        const { data } = await api.post(`${BASE}/${id}/rejeitar`, { motivo });
        return data;
    },
};

export default avaliacoesAdminService;
