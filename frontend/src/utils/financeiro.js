/**
 * Convencao monetaria do sistema: preco trafega e e armazenado em CENTAVOS
 * (inteiro). Nunca float de reais — 0.1 + 0.2 nao e 0.3.
 */

const BRL = new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
});

/** 12990 -> "R$ 129,90". Formatacao canonica do projeto. */
export function formatarCentavosParaBRL(centavos) {
    const valor = Number(centavos);
    if (!Number.isFinite(valor)) return BRL.format(0);
    return BRL.format(valor / 100);
}

/** "129,90" ou "R$ 1.299,90" -> 129990 (centavos). */
export function parseBRLParaCentavos(texto) {
    const digitos = String(texto ?? "").replace(/\D/g, "");
    return digitos ? Number(digitos) : 0;
}

/**
 * Mascara de digitacao de preco: o usuario digita so numeros e os centavos
 * "enchem" da direita para a esquerda. "1299" -> "12,99".
 */
export function mascaraPrecoCentavos(valor) {
    const digitos = String(valor ?? "")
        .replace(/\D/g, "")
        .slice(0, 11);
    const centavos = digitos.padStart(3, "0");
    const reais = centavos.slice(0, -2);
    const cents = centavos.slice(-2);
    return `${Number(reais).toLocaleString("pt-BR")},${cents}`;
}

/**
 * Acrescimo de taxa espelhando `MidpointRounding.AwayFromZero` do C# — o
 * arredondamento tem que bater com o backend ou o total diverge por 1 centavo.
 */
export function calcularValorComTaxaCentavos(valorBaseCentavos, percentual = 0, taxaFixaReais = 0) {
    const base = Number(valorBaseCentavos) || 0;
    const comPercentual = Math.round(base * (1 + (Number(percentual) || 0) / 100));
    return comPercentual + Math.round((Number(taxaFixaReais) || 0) * 100);
}

/** Soma de itens `{ precoCentavos, quantidade }`. */
export function somarItensCentavos(itens = []) {
    return itens.reduce(
        (total, item) =>
            total + (Number(item?.precoCentavos) || 0) * (Number(item?.quantidade) || 0),
        0,
    );
}

/** "em ate 6x de R$ 21,65 sem juros" */
export function formatarParcelamento(totalCentavos, parcelas) {
    if (!parcelas || parcelas < 2) return formatarCentavosParaBRL(totalCentavos);
    return `${parcelas}x de ${formatarCentavosParaBRL(Math.floor(totalCentavos / parcelas))}`;
}
