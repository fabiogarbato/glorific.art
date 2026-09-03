/**
 * Selo editorial: caixa alta minuscula, sem cantos arredondados.
 * `promocao` usa `clay` (secundaria quente) e `destaque` usa `brass` — o
 * dourado so aparece como elemento grafico, nunca como corpo de texto.
 */
const VARIANTES = {
    neutro: "border-sand bg-linen text-ink-soft",
    contorno: "border-ink/25 bg-transparent text-ink",
    promocao: "border-clay bg-clay text-bone",
    destaque: "border-brass bg-brass text-ink",
    sucesso: "border-success bg-success text-bone",
    alerta: "border-warning bg-warning text-ink",
    erro: "border-danger bg-danger text-bone",
    esgotado: "border-taupe bg-transparent text-taupe",
};

export default function Badge({ variante = "neutro", children, className = "", ...props }) {
    return (
        <span
            className={`inline-flex items-center gap-1 border px-2 py-0.5 font-sans text-xs uppercase leading-5 tracking-widest ${
                VARIANTES[variante] ?? VARIANTES.neutro
            } ${className}`}
            {...props}
        >
            {children}
        </span>
    );
}
