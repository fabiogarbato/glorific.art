import { FiAlertCircle, FiAlertTriangle, FiInfo } from "react-icons/fi";

/**
 * Bloco de aviso persistente (não é toast): fica na tela enquanto a condição
 * durar. Usado para o alerta de peso e dimensões, que precisa continuar visível
 * enquanto o admin preenche a grade.
 */
const VARIANTES = {
    info: {
        Icone: FiInfo,
        caixa: "border-sand bg-linen text-ink-soft",
        icone: "text-ink-soft",
    },
    alerta: {
        Icone: FiAlertTriangle,
        caixa: "border-warning/50 bg-warning/10 text-ink",
        icone: "text-warning",
    },
    erro: {
        Icone: FiAlertCircle,
        caixa: "border-danger/50 bg-danger/10 text-ink",
        icone: "text-danger",
    },
};

export default function Aviso({ variante = "info", titulo, children, acoes, className = "" }) {
    const { Icone, caixa, icone } = VARIANTES[variante] ?? VARIANTES.info;

    return (
        <div
            role={variante === "erro" ? "alert" : "status"}
            className={`flex flex-col gap-3 border px-4 py-3 sm:flex-row sm:items-start ${caixa} ${className}`}
        >
            <Icone size={16} className={`mt-0.5 shrink-0 ${icone}`} aria-hidden="true" />

            <div className="min-w-0 flex-1 text-sm leading-relaxed">
                {titulo && <p className="font-sans font-medium text-ink">{titulo}</p>}
                {children && <div className={titulo ? "mt-1" : ""}>{children}</div>}
            </div>

            {acoes && <div className="flex shrink-0 flex-wrap gap-2">{acoes}</div>}
        </div>
    );
}
