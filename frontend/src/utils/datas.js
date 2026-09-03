/**
 * O backend serializa DateTime em UTC sem sufixo de fuso ("2026-09-03T12:00:00").
 * `new Date()` interpretaria isso como hora LOCAL — por isso todo parse passa por
 * `paraData`, que reanexa o "Z" quando o sufixo esta ausente.
 */

const FORMATO_DATA = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short" });
const FORMATO_DATA_HORA = new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
});
const FORMATO_LONGO = new Intl.DateTimeFormat("pt-BR", {
    day: "2-digit",
    month: "long",
    year: "numeric",
});

export function paraData(valor) {
    if (!valor) return null;
    if (valor instanceof Date) return Number.isNaN(valor.getTime()) ? null : valor;

    let texto = String(valor);
    const semFuso = /^\d{4}-\d{2}-\d{2}T[\d:.]+$/.test(texto);
    if (semFuso) texto += "Z";

    const data = new Date(texto);
    return Number.isNaN(data.getTime()) ? null : data;
}

export function formatarData(valor, fallback = "—") {
    const data = paraData(valor);
    return data ? FORMATO_DATA.format(data) : fallback;
}

export function formatarDataHora(valor, fallback = "—") {
    const data = paraData(valor);
    return data ? FORMATO_DATA_HORA.format(data) : fallback;
}

export function formatarDataLonga(valor, fallback = "—") {
    const data = paraData(valor);
    return data ? FORMATO_LONGO.format(data) : fallback;
}

/** "ha 3 dias" / "em 2 horas" — usa Intl.RelativeTimeFormat, sem dependencia. */
const RELATIVO = new Intl.RelativeTimeFormat("pt-BR", { numeric: "auto" });
const DIVISORES = [
    ["second", 60],
    ["minute", 60],
    ["hour", 24],
    ["day", 7],
    ["week", 4.345],
    ["month", 12],
    ["year", Infinity],
];

export function formatarRelativo(valor, fallback = "—") {
    const data = paraData(valor);
    if (!data) return fallback;

    let delta = (data.getTime() - Date.now()) / 1000;
    for (const [unidade, limite] of DIVISORES) {
        if (Math.abs(delta) < limite) return RELATIVO.format(Math.round(delta), unidade);
        delta /= limite;
    }
    return fallback;
}

/** yyyy-MM-dd para value de `<input type="date">`. */
export function paraInputDate(valor) {
    const data = paraData(valor);
    if (!data) return "";
    const iso = new Date(data.getTime() - data.getTimezoneOffset() * 60000).toISOString();
    return iso.slice(0, 10);
}
