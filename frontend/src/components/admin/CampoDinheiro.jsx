import { useId } from "react";
import { mascaraPrecoCentavos } from "@/utils/financeiro.js";

/**
 * Dinheiro: reais na tela, CENTAVOS no fio.
 *
 * O componente é totalmente controlado pelo valor em centavos — não guarda
 * texto em estado. Digitar preenche da direita para a esquerda ("1299" vira
 * "12,99"), que é como o operador de caixa espera, e nenhuma conta em ponto
 * flutuante acontece no caminho: os dígitos viram inteiro direto.
 *
 * `valorCentavos` aceita `null` para "não informado" (o backend distingue
 * `precoComparativoCentavos: null` de zero).
 */
function paraTexto(centavos) {
    if (centavos === null || centavos === undefined || centavos === "") return "";
    return mascaraPrecoCentavos(String(Math.trunc(Number(centavos) || 0)));
}

export default function CampoDinheiro({
    label,
    valorCentavos,
    onChange,
    erro,
    ajuda,
    obrigatorio = false,
    desabilitado = false,
    id,
    className = "",
    containerClassName = "",
    ...props
}) {
    const gerado = useId();
    const campoId = id ?? gerado;
    const ajudaId = `${campoId}-ajuda`;

    const aoDigitar = (evento) => {
        const digitos = evento.target.value.replace(/\D/g, "").slice(0, 11);
        onChange?.(digitos === "" ? null : Number(digitos));
    };

    return (
        <div className={`flex flex-col gap-1.5 ${containerClassName}`}>
            {label && (
                <label htmlFor={campoId} className="eyebrow">
                    {label}
                    {obrigatorio && <span className="ml-1 text-danger">*</span>}
                </label>
            )}

            <div className="relative">
                <span
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 font-sans text-sm text-taupe"
                >
                    R$
                </span>
                <input
                    id={campoId}
                    type="text"
                    inputMode="numeric"
                    autoComplete="off"
                    disabled={desabilitado}
                    value={paraTexto(valorCentavos)}
                    onChange={aoDigitar}
                    placeholder="0,00"
                    aria-invalid={erro ? true : undefined}
                    aria-describedby={erro || ajuda ? ajudaId : undefined}
                    className={`preco w-full border bg-base-100 py-2.5 pl-10 pr-3 text-right font-sans text-base text-ink placeholder:text-taupe transition-colors focus:outline-none disabled:opacity-50 ${
                        erro ? "border-danger focus:border-danger" : "border-sand focus:border-olive"
                    } ${className}`}
                    {...props}
                />
            </div>

            {(erro || ajuda) && (
                <p
                    id={ajudaId}
                    className={`text-xs ${erro ? "text-danger" : "text-ink-soft"}`}
                    role={erro ? "alert" : undefined}
                >
                    {erro || ajuda}
                </p>
            )}
        </div>
    );
}
