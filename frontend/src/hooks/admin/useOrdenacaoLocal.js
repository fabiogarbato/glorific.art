import { useCallback, useMemo, useState } from "react";

/**
 * Ordenacao por coluna.
 *
 * Ela e LOCAL de proposito, e a tela diz isso ao usuario: nenhuma rota do
 * painel aceita parametro de ordenacao (`ProdutoService.ListarAdminAsync` fixa
 * `OrderByDescending(DataCriacao)` e o `GenericService` usa a ordenacao propria
 * de cada recurso). Ordenar aqui reorganiza a PAGINA carregada — inventar um
 * `?ordenarPor=` que o backend ignora seria pior: a seta mudaria e a lista nao.
 */
const COLLATOR = new Intl.Collator("pt-BR", { numeric: true, sensitivity: "base" });

function comparar(a, b) {
    if (a == null && b == null) return 0;
    if (a == null) return 1; // vazio sempre no fim, nas duas direcoes
    if (b == null) return -1;

    if (typeof a === "number" && typeof b === "number") return a - b;
    if (typeof a === "boolean" && typeof b === "boolean") return Number(a) - Number(b);

    return COLLATOR.compare(String(a), String(b));
}

export function useOrdenacaoLocal(itens = [], inicial = null) {
    const [ordenacao, setOrdenacao] = useState(inicial);

    /** Um clique ordena crescente, o segundo decrescente, o terceiro volta ao padrao da API. */
    const ordenar = useCallback((campo) => {
        setOrdenacao((atual) => {
            if (atual?.campo !== campo) return { campo, direcao: "asc" };
            if (atual.direcao === "asc") return { campo, direcao: "desc" };
            return null;
        });
    }, []);

    const dados = useMemo(() => {
        if (!ordenacao?.campo) return itens;
        const fator = ordenacao.direcao === "desc" ? -1 : 1;
        return [...itens].sort(
            (x, y) => fator * comparar(x?.[ordenacao.campo], y?.[ordenacao.campo]),
        );
    }, [itens, ordenacao]);

    return { ordenacao, ordenar, dados };
}

export default useOrdenacaoLocal;
