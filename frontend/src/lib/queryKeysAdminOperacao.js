/**
 * Chaves do React Query da area "painel admin — operacao".
 *
 * Ficam em arquivo proprio, e nao dentro de `lib/queryKeys.js`, para nao
 * disputar o mesmo arquivo com as outras frentes do painel. Todas comecam por
 * `queryKeys.admin.all`, entao a invalidacao por prefixo continua valendo:
 * `invalidateQueries({ queryKey: queryKeys.admin.all })` derruba tudo daqui.
 */
import { queryKeys } from "@/lib/queryKeys.js";

const raiz = queryKeys.admin.all;

export const chavesOperacao = {
    dashboard: {
        all: [...raiz, "dashboard"],
        resumo: (periodo = {}) => [...raiz, "dashboard", "resumo", periodo],
    },

    pedidos: {
        all: [...raiz, "pedidos"],
        lista: (filtros = {}) => [...raiz, "pedidos", "lista", filtros],
        detalhe: (uuid) => [...raiz, "pedidos", "detalhe", uuid],
    },

    estoque: {
        all: [...raiz, "estoque"],
        alertaMinimo: () => [...raiz, "estoque", "alerta-minimo"],
        variacao: (idVariacao) => [...raiz, "estoque", "variacao", idVariacao],
        movimentacoes: (filtros = {}) => [...raiz, "estoque", "movimentacoes", filtros],
        produtos: (filtros = {}) => [...raiz, "estoque", "produtos", filtros],
        variacoesDoProduto: (idProduto) => [...raiz, "estoque", "grade", idProduto],
    },

    cupons: {
        all: [...raiz, "cupons"],
        lista: (filtros = {}) => [...raiz, "cupons", "lista", filtros],
        detalhe: (id) => [...raiz, "cupons", "detalhe", id],
        usos: (id, pagina) => [...raiz, "cupons", "usos", id, pagina],
    },

    avaliacoes: {
        all: [...raiz, "avaliacoes"],
        lista: (filtros = {}) => [...raiz, "avaliacoes", "lista", filtros],
    },

    configuracoes: {
        all: [...raiz, "configuracoes"],
        atual: () => [...raiz, "configuracoes", "atual"],
    },

    usuarios: {
        all: [...raiz, "usuarios"],
        lista: (filtros = {}) => [...raiz, "usuarios", "lista", filtros],
        detalhe: (id) => [...raiz, "usuarios", "detalhe", id],
    },
};

export default chavesOperacao;
