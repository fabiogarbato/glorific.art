/**
 * Vocabulario da vitrine: nomes de parametro da URL, rotulos dos enums do
 * backend e as conversoes entre "estado da tela" e "query string da API".
 *
 * Mora em .js (e nao dentro das pages) por dois motivos:
 *  1. a page nunca monta query string na mao — ela le e escreve `filtros`;
 *  2. os enums do backend viajam como INTEIRO (nao ha JsonStringEnumConverter
 *     registrado no Program.cs), entao a traducao numero -> rotulo precisa de um
 *     lugar unico. Inventar rotulo espalhado pelo JSX e como o texto diverge.
 */

/** Nomes dos parametros na URL do navegador. A URL e o estado da listagem. */
export const PARAM = {
    busca: "q",
    categoria: "categoria",
    colecao: "colecao",
    genero: "genero",
    tamanhos: "tamanhos",
    cores: "cores",
    precoMin: "precoMin",
    precoMax: "precoMax",
    ordenacao: "sort",
    pagina: "page",
    esgotados: "esgotados",
};

/** 24 fecha as 3 linhas do grid de 4 colunas e as 12 do grid de 2. */
export const TAMANHO_PAGINA_CATALOGO = 24;

/** Teto de parcelas anunciado na faixa do topo. Mantenha os dois em sincronia. */
export const MAX_PARCELAS = 6;

/** OrdenacaoCatalogo (Application/DTO/Catalogo/CatalogoPublicoDtos.cs). */
export const ORDENACOES = [
    { valor: "Relevancia", rotulo: "Relevância" },
    { valor: "Novidade", rotulo: "Novidades" },
    { valor: "PrecoCrescente", rotulo: "Menor preço" },
    { valor: "PrecoDecrescente", rotulo: "Maior preço" },
    { valor: "MaisAvaliados", rotulo: "Mais avaliados" },
];

export const ORDENACAO_PADRAO = "Relevancia";

const ORDENACOES_VALIDAS = new Set(ORDENACOES.map((o) => o.valor));

/** Aliases herdados de links antigos (`/catalogo?ordem=recentes` no menu). */
const ALIAS_ORDENACAO = {
    recentes: "Novidade",
    novidades: "Novidade",
    "menor-preco": "PrecoCrescente",
    "maior-preco": "PrecoDecrescente",
};

/** GeneroProduto. O valor enviado a API e o NOME do enum, nunca o inteiro. */
export const GENEROS = [
    { valor: "Feminino", rotulo: "Feminino" },
    { valor: "Masculino", rotulo: "Masculino" },
    { valor: "Unissex", rotulo: "Unissex" },
    { valor: "Infantil", rotulo: "Infantil" },
];

const GENERO_POR_NUMERO = {
    1: "Feminino",
    2: "Masculino",
    3: "Unissex",
    4: "Infantil",
};

const MODELAGEM_POR_NUMERO = {
    1: "Justa",
    2: "Reta",
    3: "Ampla",
    4: "Oversized",
};

const GRADE_POR_NUMERO = {
    1: "Grade alfabética",
    2: "Numeração",
    3: "Tamanho único",
    4: "Infantil",
};

/** CaimentoTamanho — o dado que mais reduz devolução em moda. */
const CAIMENTO_POR_NUMERO = {
    1: "Veste muito menor",
    2: "Veste um pouco menor",
    3: "Veste como esperado",
    4: "Veste um pouco maior",
    5: "Veste muito maior",
};

/** Opções do formulário de avaliação, na ordem do menor para o maior. */
export const OPCOES_CAIMENTO = [1, 2, 3, 4, 5].map((valor) => ({
    valor,
    rotulo: CAIMENTO_POR_NUMERO[valor],
}));

export function rotuloGenero(valor) {
    return GENERO_POR_NUMERO[valor] ?? null;
}

export function rotuloModelagem(valor) {
    return MODELAGEM_POR_NUMERO[valor] ?? null;
}

export function rotuloGrade(valor) {
    return GRADE_POR_NUMERO[valor] ?? null;
}

export function rotuloCaimento(valor) {
    return CAIMENTO_POR_NUMERO[valor] ?? null;
}

/** Frase pronta para o bloco de avaliações da página de produto. */
export function recomendacaoDeTamanho(caimentoPredominante) {
    switch (caimentoPredominante) {
        case 1:
        case 2:
            return "A maioria diz que a peça veste menor — considere um número acima.";
        case 3:
            return "A maioria diz que a peça veste como esperado.";
        case 4:
        case 5:
            return "A maioria diz que a peça veste maior — considere um número abaixo.";
        default:
            return null;
    }
}

/** 4.5 -> "4,5". Nota vem como decimal e nunca deve virar "4.5" na tela. */
export function formatarNota(nota) {
    const numero = Number(nota);
    if (!Number.isFinite(numero)) return null;
    return numero.toFixed(1).replace(".", ",");
}

function inteiroOuNulo(valor) {
    if (valor === null || valor === undefined || valor === "") return null;
    const numero = Number.parseInt(valor, 10);
    return Number.isFinite(numero) ? numero : null;
}

function listaDeCsv(valor) {
    return String(valor ?? "")
        .split(",")
        .map((parte) => parte.trim())
        .filter(Boolean);
}

/**
 * Estado da listagem lido da URL. Chamada por Catalogo/Colecao a cada render:
 * a URL e a fonte da verdade, e nao um useState paralelo que sai de sincronia
 * quando o usuario aperta "voltar".
 */
export function lerFiltrosDaUrl(searchParams) {
    const bruto = searchParams.get(PARAM.ordenacao) ?? searchParams.get("ordem") ?? "";
    const ordenacao = ORDENACOES_VALIDAS.has(bruto)
        ? bruto
        : (ALIAS_ORDENACAO[bruto.toLowerCase()] ?? ORDENACAO_PADRAO);

    const pagina = inteiroOuNulo(searchParams.get(PARAM.pagina));

    return {
        busca: searchParams.get(PARAM.busca)?.trim() || "",
        categoria: searchParams.get(PARAM.categoria) || "",
        colecao: searchParams.get(PARAM.colecao) || "",
        genero: searchParams.get(PARAM.genero) || "",
        tamanhos: listaDeCsv(searchParams.get(PARAM.tamanhos)),
        cores: listaDeCsv(searchParams.get(PARAM.cores)),
        precoMin: inteiroOuNulo(searchParams.get(PARAM.precoMin)),
        precoMax: inteiroOuNulo(searchParams.get(PARAM.precoMax)),
        ordenacao,
        pagina: pagina && pagina > 0 ? pagina : 1,
        incluirEsgotados: searchParams.get(PARAM.esgotados) === "1",
    };
}

/** Filtros -> URLSearchParams. O que esta no padrao nao suja a URL. */
export function filtrosParaSearchParams(filtros) {
    const sp = new URLSearchParams();

    if (filtros.busca) sp.set(PARAM.busca, filtros.busca);
    if (filtros.categoria) sp.set(PARAM.categoria, filtros.categoria);
    if (filtros.colecao) sp.set(PARAM.colecao, filtros.colecao);
    if (filtros.genero) sp.set(PARAM.genero, filtros.genero);
    if (filtros.tamanhos?.length) sp.set(PARAM.tamanhos, filtros.tamanhos.join(","));
    if (filtros.cores?.length) sp.set(PARAM.cores, filtros.cores.join(","));
    if (filtros.precoMin) sp.set(PARAM.precoMin, String(filtros.precoMin));
    if (filtros.precoMax) sp.set(PARAM.precoMax, String(filtros.precoMax));
    if (filtros.ordenacao && filtros.ordenacao !== ORDENACAO_PADRAO) {
        sp.set(PARAM.ordenacao, filtros.ordenacao);
    }
    if (filtros.pagina && filtros.pagina > 1) sp.set(PARAM.pagina, String(filtros.pagina));
    if (filtros.incluirEsgotados) sp.set(PARAM.esgotados, "1");

    return sp;
}

/**
 * Filtros -> query string da API.
 *
 * `emEstoque` so viaja quando e `false`: o backend ja assume `true` por padrao,
 * e mandar o padrao de volta so polui a chave do React Query.
 */
export function filtrosParaApi(filtros, { pageSize = TAMANHO_PAGINA_CATALOGO } = {}) {
    return {
        q: filtros.busca || undefined,
        categoria: filtros.categoria || undefined,
        colecao: filtros.colecao || undefined,
        genero: filtros.genero || undefined,
        tamanhos: filtros.tamanhos?.length ? filtros.tamanhos.join(",") : undefined,
        cores: filtros.cores?.length ? filtros.cores.join(",") : undefined,
        precoMin: filtros.precoMin || undefined,
        precoMax: filtros.precoMax || undefined,
        sort: filtros.ordenacao || undefined,
        page: filtros.pagina && filtros.pagina > 1 ? filtros.pagina : undefined,
        pageSize,
        emEstoque: filtros.incluirEsgotados ? false : undefined,
    };
}

/** Mesmo filtro da listagem, sem paginacao — as facetas contam o conjunto todo. */
export function filtrosParaApiFacetas(filtros) {
    const { page, pageSize, sort, ...resto } = filtrosParaApi(filtros);
    void page;
    void pageSize;
    void sort;
    return resto;
}

/** Quantos filtros de refino estao ativos (o badge do botao no mobile). */
export function contarFiltrosAtivos(filtros) {
    return (
        (filtros.categoria ? 1 : 0) +
        (filtros.colecao ? 1 : 0) +
        (filtros.genero ? 1 : 0) +
        (filtros.tamanhos?.length ?? 0) +
        (filtros.cores?.length ?? 0) +
        (filtros.precoMin || filtros.precoMax ? 1 : 0) +
        (filtros.incluirEsgotados ? 1 : 0)
    );
}

/** Liga/desliga um valor dentro de um filtro de multipla escolha. */
export function alternarNaLista(lista = [], valor) {
    return lista.includes(valor) ? lista.filter((v) => v !== valor) : [...lista, valor];
}

export const FILTROS_VAZIOS = Object.freeze({
    busca: "",
    categoria: "",
    colecao: "",
    genero: "",
    tamanhos: [],
    cores: [],
    precoMin: null,
    precoMax: null,
    ordenacao: ORDENACAO_PADRAO,
    pagina: 1,
    incluirEsgotados: false,
});
