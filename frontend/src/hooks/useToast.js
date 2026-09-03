import { useContext } from "react";
import { ToastContext } from "@/contexts/ToastContext.jsx";

/**
 * `const toast = useToast(); toast.success("Salvo!")`
 *
 * Lembrete de convencao: erro de API JA vira toast no interceptor. Nao
 * re-emitir `toast.error` no catch da page — o usuario veria a mesma mensagem
 * duas vezes.
 */
export function useToast() {
    const ctx = useContext(ToastContext);
    if (!ctx) {
        throw new Error("useToast precisa estar dentro de <ToastProvider>.");
    }
    return ctx;
}

export default useToast;
