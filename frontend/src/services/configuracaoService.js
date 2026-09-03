import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

const BASE = "/admin/configuracoes";

/**
 * ConfiguracaoAdminController — /api/v1/admin/configuracoes (policy SomenteAdmin).
 *
 * Linha unica: nao ha listar, criar nem remover. O PUT invalida o cache em
 * memoria do servico no mesmo passo, entao o efeito aparece na proxima cotacao
 * de frete e nao dez minutos depois.
 */
export const configuracaoService = {
    async obter() {
        try {
            const { data } = await api.get(BASE);
            return data ?? null;
        } catch (err) {
            // 404 = a linha ainda nao foi semeada. Estado normal, nao falha.
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    async atualizar(config) {
        const { data } = await api.put(BASE, {
            freteGratisAcimaDeCentavos: config.freteGratisAcimaDeCentavos ?? null,
            prazoManuseioDias: Number(config.prazoManuseioDias) || 0,
            cepOrigem: String(config.cepOrigem ?? "").replace(/\D/g, ""),
            politicaTrocaDias: Number(config.politicaTrocaDias) || 0,
            pedidoMinimoCentavos: config.pedidoMinimoCentavos ?? null,
            exibirEstoqueBaixo: !!config.exibirEstoqueBaixo,
            limiteEstoqueBaixo: Number(config.limiteEstoqueBaixo) || 1,
        });
        return data;
    },
};

export default configuracaoService;
