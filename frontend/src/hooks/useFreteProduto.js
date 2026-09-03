import { useMutation } from "@tanstack/react-query";
import { freteService } from "@/services/freteService.js";

/**
 * Cotacao de frete da PAGINA DE PRODUTO (POST /api/v1/frete/cotacao).
 *
 * Nada a ver com `useFrete`, que cota o carrinho ja montado no servidor: aqui
 * ainda nao existe carrinho, o cliente so quer saber quanto custa para chegar
 * na casa dele antes de decidir.
 *
 * E mutation, e nao query, de proposito: a chamada custa dinheiro e tempo no
 * parceiro (2 a 5 s) e tem rate limit no backend. Ela acontece quando o cliente
 * pede — nunca sozinha a cada render nem a cada tecla digitada no CEP.
 */
export function useFreteProduto() {
    const mutation = useMutation({
        mutationFn: ({ cep, itens }) => freteService.cotar({ cep, itens }),
        retry: false, // 429 (limite) e 502 (parceiro fora do ar) nao melhoram com insistencia
    });

    return {
        cotar: mutation.mutateAsync,
        opcoes: mutation.data ?? [],
        cotando: mutation.isPending,
        cotou: mutation.isSuccess,
        /** Cotou e o parceiro nao ofereceu servico nenhum para o CEP. */
        semServico: mutation.isSuccess && (mutation.data ?? []).length === 0,
        erro: mutation.error ?? null,
        limpar: mutation.reset,
    };
}

export default useFreteProduto;
