import { FiAlertCircle, FiAlertTriangle, FiCheck, FiInfo, FiX } from "react-icons/fi";

const ESTILOS = {
    success: { classe: "border-l-success", Icone: FiCheck, cor: "text-success" },
    error: { classe: "border-l-danger", Icone: FiAlertCircle, cor: "text-danger" },
    warning: { classe: "border-l-warning", Icone: FiAlertTriangle, cor: "text-warning" },
    info: { classe: "border-l-olive", Icone: FiInfo, cor: "text-olive" },
};

/**
 * Cartao do toast. A fila e o auto-dismiss ficam no `ToastProvider` — aqui so a
 * aparencia. Sem cor de fundo saturada: filete lateral colorido sobre `bone`.
 */
export default function Toast({ type = "info", message, onClose }) {
    const { classe, Icone, cor } = ESTILOS[type] ?? ESTILOS.info;

    return (
        <div
            role={type === "error" ? "alert" : "status"}
            aria-live={type === "error" ? "assertive" : "polite"}
            className={`animate-fade-up flex w-full items-start gap-3 border border-sand border-l-4 ${classe} bg-base-100 px-4 py-3 text-left shadow-[0_10px_30px_-18px_rgba(28,26,23,0.6)]`}
        >
            <Icone size={16} className={`mt-0.5 shrink-0 ${cor}`} aria-hidden="true" />
            <p className="flex-1 text-sm leading-snug text-ink">{message}</p>
            <button
                type="button"
                onClick={onClose}
                aria-label="Fechar aviso"
                className="-mr-1 -mt-1 shrink-0 p-1 text-taupe transition-colors hover:text-ink"
            >
                <FiX size={14} />
            </button>
        </div>
    );
}
