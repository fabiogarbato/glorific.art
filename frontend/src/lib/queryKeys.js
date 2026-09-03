/**
 * Fonte unica das chaves do React Query.
 *
 * Regra: nenhum `useQuery` com array literal espalhado pelo codigo. Invalidacao
 * por prefixo funciona porque toda chave comeca pelo escopo do recurso
 * (`invalidateQueries({ queryKey: queryKeys.catalogo.all })` derruba a lista
 * inteira, filtros incluidos).
 */
export const queryKeys = {
    auth: {
        all: ["auth"],
        eu: () => [...queryKeys.auth.all, "eu"],
    },

    catalogo: {
        all: ["catalogo"],
        lista: (filtros = {}) => [...queryKeys.catalogo.all, "lista", filtros],
        facetas: (filtros = {}) => [...queryKeys.catalogo.all, "facetas", filtros],
        destaques: (limite = 8) => [...queryKeys.catalogo.all, "destaques", limite],
        produto: (slug) => [...queryKeys.catalogo.all, "produto", slug],
        relacionados: (slug, limite = 8) => [
            ...queryKeys.catalogo.all,
            "relacionados",
            slug,
            limite,
        ],
        busca: (termo) => [...queryKeys.catalogo.all, "busca", termo],
        tamanhos: (grade = null) => [...queryKeys.catalogo.all, "tamanhos", grade],
        cores: () => [...queryKeys.catalogo.all, "cores"],
    },

    avaliacoes: {
        all: ["avaliacoes"],
        doProduto: (idProduto, pagina = 1) => [
            ...queryKeys.avaliacoes.all,
            "produto",
            idProduto,
            pagina,
        ],
        resumo: (idProduto) => [...queryKeys.avaliacoes.all, "resumo", idProduto],
    },

    categorias: {
        all: ["categorias"],
        lista: () => [...queryKeys.categorias.all, "lista"],
        detalhe: (slug) => [...queryKeys.categorias.all, "detalhe", slug],
    },

    colecoes: {
        all: ["colecoes"],
        lista: () => [...queryKeys.colecoes.all, "lista"],
        detalhe: (slug) => [...queryKeys.colecoes.all, "detalhe", slug],
    },

    /**
     * Guia de medidas publico. Conteudo de cadastro, quase imutavel — por isso a
     * chave e simples: nao ha filtro nem paginacao a considerar.
     */
    tabelasMedidas: {
        all: ["tabelas-medidas"],
        lista: () => [...queryKeys.tabelasMedidas.all, "lista"],
        detalhe: (id) => [...queryKeys.tabelasMedidas.all, "detalhe", id],
    },

    carrinho: {
        all: ["carrinho"],
        atual: () => [...queryKeys.carrinho.all, "atual"],
    },

    frete: {
        all: ["frete"],
        /** Cotacao do carrinho do servidor — so o CEP entra na chave. */
        carrinho: (cep) => [...queryKeys.frete.all, "carrinho", cep],
    },

    pedidos: {
        all: ["pedidos"],
        meus: (pagina = 1) => [...queryKeys.pedidos.all, "meus", pagina],
        detalhe: (id) => [...queryKeys.pedidos.all, "detalhe", id],
        rastreio: (id) => [...queryKeys.pedidos.all, "rastreio", id],
    },

    checkout: {
        all: ["checkout"],
        status: (uuid) => [...queryKeys.checkout.all, "status", uuid],
    },

    conta: {
        all: ["conta"],
        perfil: () => [...queryKeys.conta.all, "perfil"],
        enderecos: () => [...queryKeys.conta.all, "enderecos"],
    },

    listaDesejos: {
        all: ["lista-desejos"],
        lista: () => [...queryKeys.listaDesejos.all, "lista"],
        ids: () => [...queryKeys.listaDesejos.all, "ids"],
    },

    admin: {
        all: ["admin"],
        dashboard: () => [...queryKeys.admin.all, "dashboard"],
        produtos: (filtros = {}) => [...queryKeys.admin.all, "produtos", filtros],
        estoque: () => [...queryKeys.admin.all, "estoque"],
        pedidos: (filtros = {}) => [...queryKeys.admin.all, "pedidos", filtros],
        usuarios: () => [...queryKeys.admin.all, "usuarios"],
    },
};

export default queryKeys;
