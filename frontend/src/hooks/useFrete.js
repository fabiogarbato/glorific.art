import { useCallback, useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { freteService } from "@/services/freteService.js";
import { carrinhoService } from "@/services/carrinhoService.js";
import { queryKeys } from "@/lib/queryKeys.js";
import { onlyDigits } from "@/utils/masks.js";

/**
 * Cotacao de frete. Duas rotas, dois hooks — o backend separa de proposito:
 *
 *   `useFrete`         -> POST /v1/frete/cotacao   (pagina de produto, itens avulsos)
 *   `useCotacaoFrete`  -> POST /v1/carrinho/frete  (carrinho e checkout)
 *
 * Nos dois casos `retry: false`: as falhas aqui sao 429 (limite) e 502 (parceiro
 * fora do ar), e insistir sozinho so queima mais rapido a cota paga da loja.
 */

/**
 * Simulador da pagina de produto. Nao cota sozinho — so depois que a pessoa
 * pede, porque cada consulta custa dinheiro e leva de 2 a 5 segundos.
 */
export function useFrete(itens = []) {
    const [cepConsultado, setCepConsultado] = useState("");

    const digitos = onlyDigits(cepConsultado);
    const cepValido = digitos.length === 8;
    const temItens = Array.isArray(itens) && itens.length > 0;

    const query = useQuery({
        queryKey: [
            "frete",
            "avulsa",
            digitos,
            itens.map((i) => `${i.idVariacao}x${i.quantidade ?? 1}`).join(","),
        ],
        queryFn: () => freteService.cotar({ cep: digitos, itens }),
        enabled: cepValido && temItens,
        staleTime: 1000 * 60 * 5,
        retry: false,
    });

    const cotar = useCallback((cep) => setCepConsultado(cep), []);
    const limpar = useCallback(() => setCepConsultado(""), []);

    return {
        opcoes: query.data ?? [],
        cepConsultado: digitos,
        cepValido,
        cotar,
        limpar,
        isLoading: query.isFetching,
        isError: query.isError,
        erro: query.error ?? null,
        vazio: query.isSuccess && (query.data ?? []).length === 0,
    };
}

/**
 * Cotacao do carrinho montado: os itens saem do carrinho do SERVIDOR e daqui vai
 * so o CEP. Fica em cache por CEP para que ir do carrinho ao checkout e voltar
 * nao dispare uma nova consulta paga.
 */
export function useCotacaoFrete(cep, { habilitado = true } = {}) {
    const digitos = onlyDigits(cep);
    const cepValido = digitos.length === 8;

    const query = useQuery({
        queryKey: queryKeys.frete.carrinho(digitos),
        queryFn: () => carrinhoService.cotarFrete(digitos),
        enabled: habilitado && cepValido,
        staleTime: 1000 * 60 * 5,
        retry: false,
    });

    return {
        opcoes: query.data ?? [],
        cepValido,
        isLoading: query.isFetching,
        isError: query.isError,
        erro: query.error ?? null,
        /** Ja cotou e o parceiro nao ofereceu nenhum servico para este CEP. */
        vazio: query.isSuccess && (query.data ?? []).length === 0,
        refetch: query.refetch,
    };
}

export default useFrete;
