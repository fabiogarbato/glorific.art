import api from "@/api/client.js";
import { onlyDigits } from "@/utils/masks.js";

/**
 * Cotacao de frete avulsa — FreteController (`/api/v1/frete/cotacao`).
 *
 * E a cotacao da PAGINA DE PRODUTO: publica (exigir login para simular frete e
 * o jeito mais rapido de perder a venda) e com rate limit no backend, porque
 * cada chamada vira uma consulta paga que leva de 2 a 5 segundos.
 *
 * O corpo carrega apenas `idVariacao` e `quantidade`. Peso, dimensao e valor
 * declarado saem do banco: mandar peso do navegador seria aceitar frete
 * forjado. Por isso a cotacao SO acontece com uma variacao escolhida.
 *
 * A cotacao do carrinho ja montado e outra rota (POST /api/v1/carrinho/frete) e
 * pertence a area de carrinho/checkout.
 */
export const freteService = {
    async cotar({ cep, itens = [] }) {
        const { data } = await api.post("/frete/cotacao", {
            cep: onlyDigits(cep),
            itens: itens.map((item) => ({
                idVariacao: item.idVariacao,
                quantidade: item.quantidade ?? 1,
            })),
        });

        // Ja vem ordenado por preco, com o prazo de manuseio somado.
        return Array.isArray(data) ? data : [];
    },
};

export default freteService;
