import { useRef } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { checkoutService } from "@/services/checkoutService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/** Cadencia do polling da tela de retorno. Curto: o webhook costuma chegar antes. */
const INTERVALO_MS = 3000;

/** Depois disso paramos de perguntar e oferecemos recarregar na mao. */
const JANELA_MS = 90_000;

/**
 * Fecha o pedido. A resposta traz `paymentUrl` — a pagina hospedada da
 * InfinitePay, para onde a tela redireciona o cliente.
 *
 * O carrinho vira pedido no servidor, entao o cache do carrinho e invalidado:
 * deixar o selo do cabecalho com a contagem antiga seria mentira.
 */
export function useFinalizarCheckout() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: checkoutService.finalizar,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.carrinho.all });
            queryClient.invalidateQueries({ queryKey: queryKeys.pedidos.all });
        },
    });
}

/**
 * Polling do estado REAL do pagamento.
 *
 * O cliente ter voltado da InfinitePay nao prova nada: o retorno e uma URL GET
 * que qualquer um monta. Quem aprova e o backend, depois de conferir no gateway
 * e comparar o valor. Por isso a tela le `pago` e `terminal` daqui e nunca
 * deduz aprovacao comparando strings de status.
 *
 * Para de perguntar quando o estado e terminal (pago, recusado, expirado,
 * cancelado) ou quando a janela de espera acaba.
 */
export function useStatusCheckout(uuid, { habilitado = true } = {}) {
    const inicio = useRef(Date.now());

    const query = useQuery({
        queryKey: queryKeys.checkout.status(uuid),
        queryFn: () => checkoutService.consultarStatus(uuid),
        enabled: !!uuid && habilitado,
        retry: false,
        // Enquanto o pagamento nao fecha, o dado e sempre velho.
        staleTime: 0,
        refetchInterval: (query) => {
            const dados = query.state.data;
            if (dados?.terminal) return false;
            if (Date.now() - inicio.current > JANELA_MS) return false;
            return INTERVALO_MS;
        },
        refetchIntervalInBackground: false,
    });

    const status = query.data ?? null;
    const terminal = !!status?.terminal;

    return {
        status,
        /** Verdadeiro SO quando o servidor confirmou o pagamento no gateway. */
        pago: !!status?.pago,
        terminal,
        /** Ainda pendente depois da janela de espera: hora de oferecer o botao. */
        aguardandoDemais: !!status && !terminal && Date.now() - inicio.current > JANELA_MS,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}
