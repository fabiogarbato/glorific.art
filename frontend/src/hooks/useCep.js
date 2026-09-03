import { useCallback, useState } from "react";
import { cepService } from "@/services/cepService.js";
import { onlyDigits } from "@/utils/masks.js";

/**
 * Preenchimento de endereco por CEP.
 *
 * Nao usa React Query: e uma consulta disparada por digitacao, sem cache util
 * entre telas e sem estado de servidor para invalidar.
 *
 * `naoEncontrado` e estado normal, nunca erro de sistema: CEP novo demais ou
 * ViaCEP fora do ar significam apenas "preencha na mao" — o formulario mantem
 * todos os campos editaveis.
 */
export function useCep() {
    const [buscando, setBuscando] = useState(false);
    const [naoEncontrado, setNaoEncontrado] = useState(false);

    const buscar = useCallback(async (cep) => {
        const digitos = onlyDigits(cep);
        setNaoEncontrado(false);

        if (digitos.length !== 8) return null;

        setBuscando(true);
        try {
            const encontrado = await cepService.buscar(digitos);
            setNaoEncontrado(!encontrado);
            return encontrado;
        } finally {
            setBuscando(false);
        }
    }, []);

    const limpar = useCallback(() => setNaoEncontrado(false), []);

    return { buscar, buscando, naoEncontrado, limpar };
}

export default useCep;
