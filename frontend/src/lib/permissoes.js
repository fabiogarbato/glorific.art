/**
 * Espelho, no front, das policies declaradas no backend
 * (Domain/Constants/PoliticasAutorizacao.cs + API/Configuration/AutorizacaoConfiguration.cs).
 *
 * ISTO NAO E AUTORIZACAO. Quem autoriza e o servidor — aqui so decidimos o que
 * VALE A PENA mostrar. Um item de menu escondido evita que o operador clique e
 * tome 403; ele nao impede nada.
 *
 * O papel sai do proprio JWT (claim "role" curta, emitida pelo TokenService).
 * Quando o usuario tem mais de um papel, o System.Text.Json do .NET serializa a
 * claim repetida como ARRAY — por isso o normalizador aceita string e lista.
 */
import { getToken, lerPayloadJwt, tokenValido } from "@/api/client.js";
import { CLAIM } from "@/lib/constants.js";

export const PAPEIS = {
    ADMIN: "admin",
    GERENTE: "gerente",
    OPERADOR: "operador",
    CLIENTE: "cliente",
};

/** Nomes IDENTICOS aos das policies do backend — nao inventar variacao. */
export const POLITICAS = {
    SOMENTE_ADMIN: "SomenteAdmin",
    GESTAO_CATALOGO: "GestaoCatalogo",
    EXPEDICAO: "Expedicao",
    PAINEL_ADMIN: "PainelAdmin",
};

const PAPEIS_DA_POLITICA = {
    [POLITICAS.SOMENTE_ADMIN]: [PAPEIS.ADMIN],
    [POLITICAS.GESTAO_CATALOGO]: [PAPEIS.ADMIN, PAPEIS.GERENTE],
    [POLITICAS.EXPEDICAO]: [PAPEIS.ADMIN, PAPEIS.GERENTE, PAPEIS.OPERADOR],
    [POLITICAS.PAINEL_ADMIN]: [PAPEIS.ADMIN, PAPEIS.GERENTE, PAPEIS.OPERADOR],
};

/** Papeis que abrem a porta do painel. Mesma lista de Roles.Administrativos. */
export const PAPEIS_ADMINISTRATIVOS = PAPEIS_DA_POLITICA[POLITICAS.PAINEL_ADMIN];

const ROTULO_PAPEL = {
    [PAPEIS.ADMIN]: "Administrador",
    [PAPEIS.GERENTE]: "Gerente",
    [PAPEIS.OPERADOR]: "Operador",
    [PAPEIS.CLIENTE]: "Cliente",
};

export function rotularPapel(papel) {
    const chave = String(papel ?? "").toLowerCase();
    return ROTULO_PAPEL[chave] ?? chave;
}

/** Todos os papeis atribuiveis pelo painel, na ordem de privilegio. */
export const PAPEIS_ATRIBUIVEIS = [
    PAPEIS.ADMIN,
    PAPEIS.GERENTE,
    PAPEIS.OPERADOR,
    PAPEIS.CLIENTE,
];

/** Normaliza a claim de papel: string, lista ou ausente -> array minusculo. */
export function papeisDoPayload(payload) {
    if (!payload) return [];

    const bruto = payload.role ?? payload.roles ?? payload[CLAIM.role];
    const lista = Array.isArray(bruto) ? bruto : bruto == null ? [] : [bruto];

    return [
        ...new Set(
            lista
                .map((papel) => String(papel).trim().toLowerCase())
                .filter(Boolean),
        ),
    ];
}

/** Papeis do token informado (ou do token da sessao, quando omitido). */
export function papeisDoToken(token = getToken()) {
    if (!token || !tokenValido(token)) return [];
    return papeisDoPayload(lerPayloadJwt(token));
}

/** `true` quando algum papel da lista satisfaz a policy. */
export function satisfaz(politica, papeis = []) {
    const exigidos = PAPEIS_DA_POLITICA[politica];
    // Policy desconhecida nunca libera nada: errar o nome tem que aparecer.
    if (!exigidos) return false;
    return papeis.some((papel) => exigidos.includes(papel));
}

/** Atalho sem hook — util em guarda de rota, antes de qualquer render. */
export function tokenSatisfaz(politica, token = getToken()) {
    return satisfaz(politica, papeisDoToken(token));
}

export function ehAdministrativo(papeis = []) {
    return papeis.some((papel) => PAPEIS_ADMINISTRATIVOS.includes(papel));
}

export default POLITICAS;
