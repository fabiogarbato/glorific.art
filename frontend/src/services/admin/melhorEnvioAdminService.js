import api from "@/api/client.js";

const BASE = "/admin/melhor-envio";

/**
 * Fluxo OAuth do Melhor Envio. `autorizar()` busca a URL AUTENTICADO (o endpoint exige admin) e
 * so DEPOIS o chamador navega pra ela — uma navegacao pura do browser nao carrega o Bearer
 * token, entao nao dava pra so linkar direto pra `/autorizar`.
 */
export const melhorEnvioAdminService = {
    async autorizar() {
        const { data } = await api.get(`${BASE}/autorizar`);
        return data?.url;
    },

    async conectar(code, state) {
        await api.post(`${BASE}/conectar`, { code, state });
    },

    async status() {
        const { data } = await api.get(`${BASE}/status`);
        return data;
    },
};

export default melhorEnvioAdminService;
