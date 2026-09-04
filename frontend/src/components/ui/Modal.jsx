import { createPortal } from "react-dom";
import { FiX } from "react-icons/fi";
import { useDialog } from "@/hooks/useDialog.js";

const LARGURAS = {
    sm: "max-w-sm",
    md: "max-w-md",
    lg: "max-w-2xl",
    xl: "max-w-4xl",
};

/**
 * Contrato de modal do projeto: `{ isOpen, onClose }` + `if (!isOpen) return null`.
 * Montar so quando aberto e o que mantem o `useDialog` correto (foco entra ao
 * abrir, volta ao fechar).
 */
export default function Modal({
    isOpen,
    onClose,
    titulo,
    children,
    rodape,
    largura = "md",
    fecharNoBackdrop = true,
}) {
    const { panelRef, dialogProps } = useDialog(isOpen, onClose);

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 z-overlay flex items-end justify-center bg-ink/40 p-0 sm:items-center sm:p-6">
            <button
                type="button"
                aria-label="Fechar"
                tabIndex={-1}
                className="absolute inset-0 h-full w-full cursor-default"
                onClick={fecharNoBackdrop ? onClose : undefined}
            />

            <div
                ref={panelRef}
                {...dialogProps}
                aria-label={typeof titulo === "string" ? titulo : "Janela"}
                className={`relative flex w-full ${LARGURAS[largura] ?? LARGURAS.md} max-h-[90vh] flex-col border border-sand bg-base-100 shadow-[0_18px_60px_-24px_rgba(28,26,23,0.45)]`}
            >
                <header className="flex shrink-0 items-start justify-between gap-4 border-b border-sand px-6 py-4">
                    <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label="Fechar"
                        className="-mr-2 -mt-1 flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-ink"
                    >
                        <FiX size={18} />
                    </button>
                </header>

                {/* So o corpo rola. Cabecalho e rodape ficam fixos no lugar deles —
                    e por isso que da pra sempre ver o titulo e os botoes de acao,
                    mesmo num formulario comprido como o de colecao. */}
                <div className="overflow-y-auto px-6 py-5 text-base text-ink-soft">{children}</div>

                {rodape && (
                    <footer className="flex shrink-0 flex-wrap justify-end gap-3 border-t border-sand bg-linen px-6 py-4">
                        {rodape}
                    </footer>
                )}
            </div>
        </div>,
        document.body,
    );
}
