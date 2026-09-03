/**
 * Periodo do painel.
 *
 * O backend grava e compara tudo em UTC e faz `DateTime.SpecifyKind(..., Utc)`
 * nos filtros. Se mandassemos "2026-09-01T00:00:00Z", o binder do ASP.NET
 * converteria para a hora local do HOST antes do SpecifyKind e o relatorio
 * perderia (ou duplicaria) as primeiras horas do dia. Por isso
 * `paraParametroUtc` emite o instante JA EM UTC e SEM sufixo de fuso — que e
 * exatamente o formato que o `utils/datas.js` sabe reler.
 */

const DOIS = (n) => String(n).padStart(2, "0");

/** Date -> "2026-09-01T00:00:00" com os campos ja em UTC. */
export function paraParametroUtc(data) {
    if (!data) return undefined;
    const d = data instanceof Date ? data : new Date(data);
    if (Number.isNaN(d.getTime())) return undefined;

    return (
        `${d.getUTCFullYear()}-${DOIS(d.getUTCMonth() + 1)}-${DOIS(d.getUTCDate())}` +
        `T${DOIS(d.getUTCHours())}:${DOIS(d.getUTCMinutes())}:${DOIS(d.getUTCSeconds())}`
    );
}

/** "2026-09-01" (value de <input type="date">) -> 00:00:00 local daquele dia. */
export function inicioDoDiaLocal(texto) {
    if (!texto) return null;
    const [ano, mes, dia] = String(texto).split("-").map(Number);
    if (!ano || !mes || !dia) return null;
    return new Date(ano, mes - 1, dia, 0, 0, 0, 0);
}

/** Mesmo texto -> 23:59:59 local. O fim do periodo tem que INCLUIR o dia. */
export function fimDoDiaLocal(texto) {
    if (!texto) return null;
    const [ano, mes, dia] = String(texto).split("-").map(Number);
    if (!ano || !mes || !dia) return null;
    return new Date(ano, mes - 1, dia, 23, 59, 59, 999);
}

function inicioDeHoje() {
    const agora = new Date();
    return new Date(agora.getFullYear(), agora.getMonth(), agora.getDate(), 0, 0, 0, 0);
}

function fimDeHoje() {
    const agora = new Date();
    return new Date(agora.getFullYear(), agora.getMonth(), agora.getDate(), 23, 59, 59, 999);
}

function diasAtras(dias) {
    const base = inicioDeHoje();
    base.setDate(base.getDate() - dias);
    return base;
}

/**
 * Presets do seletor. `personalizado` nao calcula nada — a tela usa os dois
 * `<input type="date">`.
 */
export const PRESETS_PERIODO = [
    { chave: "hoje", rotulo: "Hoje" },
    { chave: "7dias", rotulo: "7 dias" },
    { chave: "30dias", rotulo: "30 dias" },
    { chave: "mesAtual", rotulo: "Mês atual" },
    { chave: "mesAnterior", rotulo: "Mês anterior" },
    { chave: "90dias", rotulo: "90 dias" },
    { chave: "personalizado", rotulo: "Personalizado" },
];

export const PRESET_PADRAO = "30dias";

/** Preset -> `{ de: Date, ate: Date }` em hora LOCAL. */
export function intervaloDoPreset(chave) {
    const agora = new Date();

    switch (chave) {
        case "hoje":
            return { de: inicioDeHoje(), ate: fimDeHoje() };

        case "7dias":
            return { de: diasAtras(6), ate: fimDeHoje() };

        case "90dias":
            return { de: diasAtras(89), ate: fimDeHoje() };

        case "mesAtual":
            return {
                de: new Date(agora.getFullYear(), agora.getMonth(), 1, 0, 0, 0, 0),
                ate: fimDeHoje(),
            };

        case "mesAnterior": {
            const primeiro = new Date(agora.getFullYear(), agora.getMonth() - 1, 1, 0, 0, 0, 0);
            const ultimo = new Date(agora.getFullYear(), agora.getMonth(), 0, 23, 59, 59, 999);
            return { de: primeiro, ate: ultimo };
        }

        case "30dias":
        default:
            return { de: diasAtras(29), ate: fimDeHoje() };
    }
}

/** yyyy-MM-dd em hora local, para preencher `<input type="date">`. */
export function paraInputDateLocal(data) {
    if (!data) return "";
    const d = data instanceof Date ? data : new Date(data);
    if (Number.isNaN(d.getTime())) return "";
    return `${d.getFullYear()}-${DOIS(d.getMonth() + 1)}-${DOIS(d.getDate())}`;
}
