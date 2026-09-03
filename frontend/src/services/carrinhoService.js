/**
 * Carrinho server-side (`/api/v1/carrinho`).
 *
 * IDENTIDADE — o backend resolve o dono da requisicao em duas fontes, nesta ordem:
 *   1. claim `sub` do Bearer (cliente logado);
 *   2. cookie httpOnly `gl_cart`, emitido pelo proprio backend no primeiro POST.
 * Nenhuma rota aceita id de carrinho vindo do cliente. Por isso o front NUNCA
 * manda identificador de carrinho — e por isso toda chamada leva
 * `withCredentials: true`: sem o cookie na requisicao o visitante anonimo perde
 * o carrinho a cada refresh.
 *
 * DINHEIRO — tudo em centavos (int). Formatacao so na borda da tela, com
 * `utils/financeiro.js`. Nenhuma conta com float aqui.
 */
import api from "@/api/client.js";

/** Toda chamada de carrinho precisa enviar/receber o cookie `gl_cart`. */
const COM_COOKIE = { withCredentials: true };

/** Carrinho neutro — o que a UI desenha antes da primeira resposta e no 404. */
export const CARRINHO_VAZIO = Object.freeze({
    uuid: null,
    itens: [],
    quantidadeItens: 0,
    subtotalCentavos: 0,
    descontoCentavos: 0,
    totalCentavos: 0,
    codigoCupom: null,
    freteGratisPorCupom: false,
    avisoCupom: null,
    possuiItemIndisponivel: false,
    possuiPrecoAlterado: false,
    pesoTotalGramas: 0,
    expiraEm: null,
});

/**
 * Blinda a tela contra resposta parcial: o componente pode confiar que `itens`
 * e array e que todo campo de dinheiro e numero.
 */
function normalizar(data) {
    if (!data) return { ...CARRINHO_VAZIO };

    return {
        ...CARRINHO_VAZIO,
        ...data,
        itens: Array.isArray(data.itens) ? data.itens : [],
        quantidadeItens: Number(data.quantidadeItens) || 0,
        subtotalCentavos: Number(data.subtotalCentavos) || 0,
        descontoCentavos: Number(data.descontoCentavos) || 0,
        totalCentavos: Number(data.totalCentavos) || 0,
    };
}

export const carrinhoService = {
    // GET /api/v1/carrinho — nao cria nada; sem carrinho devolve carrinho vazio.
    async obter() {
        const { data } = await api.get("/carrinho", COM_COOKIE);
        return normalizar(data);
    },

    // POST /api/v1/carrinho/itens — soma a quantidade quando a variacao ja esta la.
    async adicionarItem({ idVariacao, quantidade = 1 }) {
        const { data } = await api.post(
            "/carrinho/itens",
            { idVariacao: Number(idVariacao), quantidade: Number(quantidade) },
            COM_COOKIE,
        );
        return normalizar(data);
    },

    // PATCH /api/v1/carrinho/itens/{idItem} — quantidade zero remove a linha.
    async alterarQuantidade(idItem, quantidade) {
        const { data } = await api.patch(
            `/carrinho/itens/${idItem}`,
            { quantidade: Number(quantidade) },
            COM_COOKIE,
        );
        return normalizar(data);
    },

    // DELETE /api/v1/carrinho/itens/{idItem}
    async removerItem(idItem) {
        const { data } = await api.delete(`/carrinho/itens/${idItem}`, COM_COOKIE);
        return normalizar(data);
    },

    // DELETE /api/v1/carrinho — esvazia e solta o cupom.
    async esvaziar() {
        const { data } = await api.delete("/carrinho", COM_COOKIE);
        return normalizar(data);
    },

    /**
     * POST /api/v1/carrinho/merge — funde o carrinho anonimo no do usuario.
     * Exige Bearer. A chave anonima sai do cookie, nunca do corpo.
     */
    async mesclar() {
        const { data } = await api.post("/carrinho/merge", null, COM_COOKIE);
        return normalizar(data);
    },

    // POST /api/v1/carrinho/cupom — previa de desconto; quem valida de verdade e o checkout.
    async aplicarCupom(codigo) {
        const { data } = await api.post(
            "/carrinho/cupom",
            { codigo: String(codigo ?? "").trim().toUpperCase() },
            COM_COOKIE,
        );
        return normalizar(data);
    },

    // DELETE /api/v1/carrinho/cupom
    async removerCupom() {
        const { data } = await api.delete("/carrinho/cupom", COM_COOKIE);
        return normalizar(data);
    },

    /**
     * POST /api/v1/carrinho/frete — os itens saem do carrinho do SERVIDOR; daqui vai
     * so o CEP. Tem rate limit proprio no backend (cada cotacao e uma consulta
     * paga no Melhor Envio), entao nada de cotar a cada tecla.
     */
    async cotarFrete(cep) {
        const { data } = await api.post(
            "/carrinho/frete",
            { cep: String(cep ?? "").replace(/\D/g, "") },
            COM_COOKIE,
        );
        return Array.isArray(data) ? data : [];
    },
};

export default carrinhoService;
