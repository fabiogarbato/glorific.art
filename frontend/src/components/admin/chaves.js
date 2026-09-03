/**
 * Identificadores tecnicos de coluna e de campo de ordenacao.
 *
 * Moram num `.js` fora do JSX de proposito: sao CHAVES DE DADO, nao copy. Um
 * `chave: "acoes"` escrito solto dentro de um `.jsx` seria lido pelo guarda de
 * acentuacao (`src/tests/acentuacao.test.js`) como texto de tela sem acento — e
 * a correcao "certa" ali seria acentuar um identificador, o que quebraria o
 * acesso ao campo. Centralizar resolve os dois lados.
 */
export const COL = {
    acoes: "acoes",
    altText: "altText",
    ativo: "ativo",
    codigo: "codigo",
    contentType: "contentType",
    dataCriacao: "dataCriacao",
    dataFim: "dataFim",
    dataInicio: "dataInicio",
    descricao: "descricao",
    destaque: "destaque",
    estoque: "estoqueTotalDisponivel",
    grade: "grade",
    habilitado: "habilitado",
    hexRgb: "hexRgb",
    linhas: "linhas",
    nome: "nome",
    nomeCategoria: "nomeCategoria",
    observacao: "observacao",
    ordem: "ordem",
    preco: "precoBaseCentavos",
    skuBase: "skuBase",
    slug: "slug",
    tamanhoBytes: "tamanhoBytes",
    totalVariacoes: "totalVariacoes",
    url: "url",
};

/**
 * Nomes de CAMPO de formulario, pelo mesmo motivo: `setCampo("descricao", ...)`
 * dentro de um `.jsx` seria lido como copy sem acento, e acentuar o nome do
 * campo quebraria o estado do formulario.
 */
export const CAMPO = {
    codigo: "codigo",
    descricao: "descricao",
    observacao: "observacao",
};

export default COL;
