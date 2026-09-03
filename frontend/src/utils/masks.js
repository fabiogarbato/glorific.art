/**
 * Padrao do projeto: formatar no `onChange` com `onlyDigits` + montagem manual da
 * string e `maxLength` no input; antes de enviar a API, limpar a mascara com
 * `onlyDigits`. A validacao forte tambem e feita na borda da API.
 */

export const LIMITE_TEXTO_LIVRE = 255;
export const SENHA_MIN = 8;
export const SENHA_MAX = 150;

export const CPF_MAXLENGTH = 14; // 000.000.000-00
export const TELEFONE_MAXLENGTH = 15; // (00) 00000-0000
export const CEP_MAXLENGTH = 9; // 00000-000
export const EMAIL_MAXLENGTH = 254;

export function onlyDigits(value) {
    return String(value ?? "").replace(/\D/g, "");
}

/** 000.000.000-00 */
export function formatCPF(value) {
    const d = onlyDigits(value).slice(0, 11);
    if (d.length <= 3) return d;
    if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
    if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;
    return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** (00) 0000-0000 ate 8 digitos; (00) 00000-0000 com 9. */
export function formatTelefone(value) {
    const d = onlyDigits(value).slice(0, 11);
    if (d.length <= 2) return d.length ? `(${d}` : "";
    if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
    if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
    return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

/** 00000-000 */
export function formatCEP(value) {
    const d = onlyDigits(value).slice(0, 8);
    if (d.length <= 5) return d;
    return `${d.slice(0, 5)}-${d.slice(5)}`;
}

export function normalizeEmail(value) {
    return String(value ?? "")
        .trim()
        .toLowerCase();
}

export function isValidEmail(value) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizeEmail(value));
}

export function isValidCEP(value) {
    return onlyDigits(value).length === 8;
}

export function isValidTelefone(value) {
    const d = onlyDigits(value);
    return d.length === 10 || d.length === 11;
}

/** CPF com os dois digitos verificadores; rejeita sequencias repetidas. */
export function isValidCPF(value) {
    const cpf = onlyDigits(value);
    if (cpf.length !== 11) return false;
    if (/^(\d)\1{10}$/.test(cpf)) return false;

    const digito = (ate) => {
        let soma = 0;
        for (let i = 0; i < ate; i++) soma += Number(cpf[i]) * (ate + 1 - i);
        const resto = (soma * 10) % 11;
        return resto === 10 ? 0 : resto;
    };

    return digito(9) === Number(cpf[9]) && digito(10) === Number(cpf[10]);
}

// Aliases em ingles mantidos por conveniencia de quem vem do repo de referencia.
export const formatPhone = formatTelefone;
export const formatCep = formatCEP;
export const isValidCep = isValidCEP;
