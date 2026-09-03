import { useEffect, useRef } from "react";

const FOCAVEIS = [
    "a[href]",
    "button:not([disabled])",
    "input:not([disabled])",
    "select:not([disabled])",
    "textarea:not([disabled])",
    '[tabindex]:not([tabindex="-1"])',
].join(",");

/**
 * Acessibilidade de overlay, replicada em TODO modal/drawer do projeto:
 * Esc fecha, o foco entra no painel e volta para quem abriu, Tab fica preso
 * dentro e o body trava o scroll (compensando a largura da barra para a pagina
 * nao "pular").
 *
 * Uso:
 *   const { panelRef, dialogProps } = useDialog(isOpen, onClose);
 *   <div ref={panelRef} {...dialogProps} aria-label="Carrinho"> ... </div>
 */
export function useDialog(isOpen, onClose) {
    const panelRef = useRef(null);
    const origemFocoRef = useRef(null);

    useEffect(() => {
        if (!isOpen) return undefined;

        origemFocoRef.current = document.activeElement;

        const painel = panelRef.current;
        if (painel) {
            const primeiro = painel.querySelector(FOCAVEIS);
            (primeiro ?? painel).focus?.({ preventScroll: true });
        }

        const onKeyDown = (e) => {
            if (e.key === "Escape") {
                e.stopPropagation();
                onClose?.();
                return;
            }
            if (e.key !== "Tab" || !panelRef.current) return;

            const focaveis = Array.from(panelRef.current.querySelectorAll(FOCAVEIS)).filter(
                (el) => el.offsetParent !== null,
            );
            if (focaveis.length === 0) {
                e.preventDefault();
                return;
            }

            const primeiro = focaveis[0];
            const ultimo = focaveis[focaveis.length - 1];
            if (e.shiftKey && document.activeElement === primeiro) {
                e.preventDefault();
                ultimo.focus();
            } else if (!e.shiftKey && document.activeElement === ultimo) {
                e.preventDefault();
                primeiro.focus();
            }
        };

        document.addEventListener("keydown", onKeyDown, true);

        // Scroll-lock com compensacao da scrollbar.
        const overflowAnterior = document.body.style.overflow;
        const paddingAnterior = document.body.style.paddingRight;
        const larguraBarra = window.innerWidth - document.documentElement.clientWidth;
        document.body.style.overflow = "hidden";
        if (larguraBarra > 0) document.body.style.paddingRight = `${larguraBarra}px`;

        return () => {
            document.removeEventListener("keydown", onKeyDown, true);
            document.body.style.overflow = overflowAnterior;
            document.body.style.paddingRight = paddingAnterior;
            origemFocoRef.current?.focus?.({ preventScroll: true });
        };
    }, [isOpen, onClose]);

    return {
        panelRef,
        dialogProps: { role: "dialog", "aria-modal": "true", tabIndex: -1 },
    };
}

export default useDialog;
