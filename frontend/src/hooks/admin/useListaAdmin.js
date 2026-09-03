import { useCallback, useMemo, useRef, useState } from "react";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";

/**
 * Estado de uma listagem administrativa: filtros, pagina e tamanho de pagina.
 *
 * A regra que este hook existe para garantir: MUDAR FILTRO RESETA A PAGINA.
 * Sem isso o usuario filtra estando na pagina 4 e recebe uma lista vazia que
 * parece "nao encontrou nada".
 */
export function useListaAdmin(filtrosIniciais = {}, tamanhoInicial = ITENS_POR_PAGINA) {
    // Congela o objeto da primeira render: literal inline mudaria de identidade
    // a cada ciclo e `limpar` viraria uma funcao instavel.
    const iniciaisRef = useRef(filtrosIniciais);

    const [filtros, setFiltros] = useState(iniciaisRef.current);
    const [pagina, setPagina] = useState(1);
    const [tamanhoPagina, setTamanhoPaginaState] = useState(tamanhoInicial);

    const definirFiltro = useCallback((campo, valor) => {
        setFiltros((atual) => ({ ...atual, [campo]: valor }));
        setPagina(1);
    }, []);

    const definirFiltros = useCallback((novos) => {
        setFiltros((atual) => ({ ...atual, ...novos }));
        setPagina(1);
    }, []);

    const definirTamanhoPagina = useCallback((valor) => {
        setTamanhoPaginaState(Number(valor) || ITENS_POR_PAGINA);
        setPagina(1);
    }, []);

    const limpar = useCallback(() => {
        setFiltros(iniciaisRef.current);
        setPagina(1);
    }, []);

    const params = useMemo(
        () => ({ ...filtros, pagina, tamanhoPagina }),
        [filtros, pagina, tamanhoPagina],
    );

    return {
        filtros,
        pagina,
        tamanhoPagina,
        params,
        setPagina,
        definirFiltro,
        definirFiltros,
        definirTamanhoPagina,
        limpar,
    };
}

export default useListaAdmin;
