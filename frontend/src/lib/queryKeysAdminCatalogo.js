/**
 * Chaves do React Query da area "painel admin -> catalogo".
 *
 * Mora em arquivo proprio (e nao dentro de `lib/queryKeys.js`) porque varias
 * frentes escrevem no projeto ao mesmo tempo e um unico arquivo de chaves vira
 * ponto de conflito. O prefixo continua sendo `queryKeys.admin.all`, entao
 * `invalidateQueries({ queryKey: queryKeys.admin.all })` derruba tudo daqui
 * junto — a invalidacao por prefixo segue valendo.
 */
import { queryKeys } from "@/lib/queryKeys.js";

const RAIZ = queryKeys.admin.all; // ["admin"]

const escopo = (recurso) => [...RAIZ, recurso];

export const chavesCatalogo = {
    produtos: {
        all: escopo("produtos"),
        lista: (filtros = {}) => [...escopo("produtos"), "lista", filtros],
        detalhe: (id) => [...escopo("produtos"), "detalhe", Number(id) || 0],
        variacoes: (id, incluirInativas = false) => [
            ...escopo("produtos"),
            "variacoes",
            Number(id) || 0,
            !!incluirInativas,
        ],
        galeria: (id) => [...escopo("produtos"), "galeria", Number(id) || 0],
        logs: (id, filtros = {}) => [...escopo("produtos"), "logs", Number(id) || 0, filtros],
    },

    categorias: {
        all: escopo("categorias"),
        lista: (filtros = {}) => [...escopo("categorias"), "lista", filtros],
        arvore: (somenteHabilitadas = false) => [
            ...escopo("categorias"),
            "arvore",
            !!somenteHabilitadas,
        ],
    },

    colecoes: {
        all: escopo("colecoes"),
        lista: (filtros = {}) => [...escopo("colecoes"), "lista", filtros],
    },

    tamanhos: {
        all: escopo("tamanhos"),
        lista: (filtros = {}) => [...escopo("tamanhos"), "lista", filtros],
        ativos: (grade = null) => [...escopo("tamanhos"), "ativos", grade ?? "todas"],
    },

    cores: {
        all: escopo("cores"),
        lista: (filtros = {}) => [...escopo("cores"), "lista", filtros],
        ativas: () => [...escopo("cores"), "ativas"],
    },

    midias: {
        all: escopo("midias"),
        lista: (filtros = {}) => [...escopo("midias"), "lista", filtros],
    },

    tabelasMedidas: {
        all: escopo("tabelas-medidas"),
        lista: (filtros = {}) => [...escopo("tabelas-medidas"), "lista", filtros],
        detalhe: (id) => [...escopo("tabelas-medidas"), "detalhe", Number(id) || 0],
    },
};

export default chavesCatalogo;
