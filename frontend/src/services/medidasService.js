import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

/**
 * Guia de medidas publico — `/api/v1/tabelas-medidas`.
 *
 * Contrato acordado com o backend (endpoint [AllowAnonymous], devolve apenas
 * tabelas ATIVAS e ja com as linhas ordenadas):
 *
 *   GET /api/v1/tabelas-medidas
 *     -> [ { id, nome, observacao, linhas: [ { idTamanho, codigoTamanho,
 *            ordemTamanho, bustoCm, cinturaCm, quadrilCm, comprimentoCm,
 *            mangaCm } ] } ]
 *   GET /api/v1/tabelas-medidas/{id}
 *     -> o mesmo objeto | 404 quando nao existe ou esta inativa
 *
 * Convencoes desta camada (as mesmas do resto do front):
 *  - o caminho NAO repete `/v1`: a baseURL do client ja e `/api/v1`;
 *  - 404 e estado de dominio ("nao existe"), entao vira `null` em vez de erro;
 *  - a ordenacao das linhas e refeita aqui mesmo. O servidor ja manda ordenado,
 *    mas a tela nao pode depender disso: tamanho fora de ordem ("GG, P, M") faz
 *    a pessoa ler a linha errada e comprar o numero errado.
 */

const BASE = "/tabelas-medidas";

/** Numero ou `null` — string vazia, `undefined` e lixo viram ausencia de medida. */
function medida(valor) {
    if (valor === null || valor === undefined || valor === "") return null;
    const numero = Number(valor);
    return Number.isFinite(numero) ? numero : null;
}

/**
 * Uma linha da tabela, no vocabulario da tela.
 *
 * `ordemTamanho` e o campo do contrato; `ordem` entra so como rede de seguranca
 * porque e o nome usado no DTO do admin — se um dia o publico for gerado a
 * partir dele, a tela nao passa a listar em ordem aleatoria por causa disso.
 */
function normalizarLinha(linha) {
    return {
        idTamanho: linha?.idTamanho ?? null,
        codigoTamanho: linha?.codigoTamanho ?? "",
        ordemTamanho: linha?.ordemTamanho ?? linha?.ordem ?? 0,
        bustoCm: medida(linha?.bustoCm),
        cinturaCm: medida(linha?.cinturaCm),
        quadrilCm: medida(linha?.quadrilCm),
        comprimentoCm: medida(linha?.comprimentoCm),
        mangaCm: medida(linha?.mangaCm),
    };
}

function normalizarTabela(tabela) {
    if (!tabela) return null;

    const linhas = (Array.isArray(tabela.linhas) ? tabela.linhas : [])
        .map(normalizarLinha)
        .sort((a, b) => a.ordemTamanho - b.ordemTamanho);

    return {
        id: tabela.id,
        nome: tabela.nome ?? "",
        observacao: tabela.observacao ?? null,
        linhas,
    };
}

export const medidasService = {
    /** Todas as tabelas ativas. Lista vazia e resposta legitima: loja sem cadastro. */
    async listar() {
        const { data } = await api.get(BASE);
        return (Array.isArray(data) ? data : []).map(normalizarTabela).filter(Boolean);
    },

    /** Uma tabela pelo id. `null` quando nao existe ou foi desativada. */
    async obter(id) {
        try {
            const { data } = await api.get(`${BASE}/${id}`);
            return normalizarTabela(data);
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },
};

export default medidasService;
