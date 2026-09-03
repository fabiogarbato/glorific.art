/**
 * Enums do catalogo, espelhando `Domain/Enums/Enums.cs`.
 *
 * O backend NAO usa `JsonStringEnumConverter`: os enums trafegam como INTEIRO.
 * Mandar "Feminino" em vez de 1 quebra a desserializacao — por isso o valor
 * daqui vai cru para o wire e o rotulo so existe para a tela.
 */

export const GENERO_PRODUTO = {
    FEMININO: 1,
    MASCULINO: 2,
    UNISSEX: 3,
    INFANTIL: 4,
};

export const GENEROS = [
    { valor: GENERO_PRODUTO.FEMININO, rotulo: "Feminino" },
    { valor: GENERO_PRODUTO.MASCULINO, rotulo: "Masculino" },
    { valor: GENERO_PRODUTO.UNISSEX, rotulo: "Unissex" },
    { valor: GENERO_PRODUTO.INFANTIL, rotulo: "Infantil" },
];

export const MODELAGEM_PRODUTO = {
    JUSTA: 1,
    RETA: 2,
    AMPLA: 3,
    OVERSIZED: 4,
};

export const MODELAGENS = [
    { valor: MODELAGEM_PRODUTO.JUSTA, rotulo: "Justa" },
    { valor: MODELAGEM_PRODUTO.RETA, rotulo: "Reta" },
    { valor: MODELAGEM_PRODUTO.AMPLA, rotulo: "Ampla" },
    { valor: MODELAGEM_PRODUTO.OVERSIZED, rotulo: "Oversized" },
];

export const GRADE_TAMANHO = {
    ALFA: 1,
    NUMERICA: 2,
    UNICO: 3,
    INFANTIL: 4,
};

export const GRADES_TAMANHO = [
    { valor: GRADE_TAMANHO.ALFA, rotulo: "Alfabética" },
    { valor: GRADE_TAMANHO.NUMERICA, rotulo: "Numérica" },
    { valor: GRADE_TAMANHO.UNICO, rotulo: "Tamanho único" },
    { valor: GRADE_TAMANHO.INFANTIL, rotulo: "Infantil" },
];

const indexar = (lista) =>
    Object.fromEntries(lista.map(({ valor, rotulo }) => [valor, rotulo]));

const ROTULOS_GENERO = indexar(GENEROS);
const ROTULOS_MODELAGEM = indexar(MODELAGENS);
const ROTULOS_GRADE = indexar(GRADES_TAMANHO);

export const rotuloGenero = (valor) => ROTULOS_GENERO[valor] ?? "—";
export const rotuloModelagem = (valor) => ROTULOS_MODELAGEM[valor] ?? "—";
export const rotuloGrade = (valor) => ROTULOS_GRADE[valor] ?? "—";

/** Limites de tamanho de campo copiados dos `[StringLength]` dos DTOs. */
export const LIMITES = {
    produtoNome: 180,
    produtoSlug: 200,
    produtoSkuBase: 60,
    composicaoTecido: 400,
    metaTitle: 200,
    metaDescription: 400,
    categoriaNome: 180,
    colecaoNome: 180,
    colecaoEpigrafe: 400,
    corNome: 80,
    corSlug: 100,
    tamanhoCodigo: 10,
    tamanhoDescricao: 120,
    variacaoSku: 60,
    codigoBarras: 20,
    tabelaNome: 120,
    altText: 300,
};

/** Tetos dos `[Range]` de peso e dimensao no `ProdutoVariacaoCreateDto`. */
export const LIMITES_LOGISTICA = {
    pesoMinimo: 1,
    pesoMaximo: 100000,
    dimensaoMinima: 0.01,
    dimensaoMaxima: 999999.99,
};

export const HEX_VALIDO = /^#[0-9a-fA-F]{6}$/;
