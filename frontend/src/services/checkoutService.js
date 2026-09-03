/**
 * Fechamento de compra (`/api/v1/checkout`).
 *
 * O corpo do POST carrega SO escolha: qual endereco e qual servico de frete.
 * Preco, frete, desconto e total sao recalculados no servidor — mandar valor
 * daqui seria oferecer frete gratis a quem edita o devtools.
 *
 * A resposta traz `paymentUrl`: a pagina hospedada da InfinitePay. O cliente sai
 * do site e volta em `/checkout/retorno`. Quem decide se o pagamento foi aprovado
 * e o backend, depois de conferir no gateway — nunca esta camada.
 */
import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";
import { CHECKOUT_UUID_KEY } from "@/lib/constants.js";

/** Guarda o uuid do pedido recem-criado para a tela de retorno saber o que consultar. */
export function lembrarCheckout(uuid) {
    try {
        if (uuid) sessionStorage.setItem(CHECKOUT_UUID_KEY, uuid);
        else sessionStorage.removeItem(CHECKOUT_UUID_KEY);
    } catch {
        /* storage bloqueado — a tela de retorno cai no ultimo pedido do cliente */
    }
}

export function lerCheckoutLembrado() {
    try {
        return sessionStorage.getItem(CHECKOUT_UUID_KEY);
    } catch {
        return null;
    }
}

export const checkoutService = {
    /**
     * POST /api/v1/checkout — 201 com o pedido criado e a URL de pagamento.
     * @param {{ idEndereco:number, idServicoFrete:number, codigoCupom?:string, observacaoCliente?:string }} escolha
     */
    async finalizar(escolha) {
        const corpo = {
            idEndereco: Number(escolha?.idEndereco),
            idServicoFrete: Number(escolha?.idServicoFrete),
            codigoCupom: escolha?.codigoCupom?.trim() || null,
            observacaoCliente: escolha?.observacaoCliente?.trim() || null,
        };

        const { data } = await api.post("/checkout", corpo);

        lembrarCheckout(data?.uuid);

        return data;
    },

    /**
     * GET /api/v1/checkout/{uuid}/status — alvo do polling da tela de retorno.
     * `pago` e `terminal` sao derivados no servidor: a tela nunca deve inferir
     * pagamento comparando strings de status.
     */
    async consultarStatus(uuid) {
        try {
            const { data } = await api.get(`/checkout/${uuid}/status`);
            return data ?? null;
        } catch (err) {
            // 404 = uuid desconhecido ou de outra pessoa. E estado, nao falha.
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },
};

export default checkoutService;
