import { createContext, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toastBus } from "@/api/toastBus.js";
import Toast from "@/components/ui/Toast.jsx";

export const ToastContext = createContext(null);

const DURACAO_MS = 4000;

/**
 * Fila de toasts + assinatura do `toastBus`.
 * E aqui que o interceptor do axios ganha voz na interface.
 */
export function ToastProvider({ children }) {
    const [toasts, setToasts] = useState([]);
    const seq = useRef(0);
    const timers = useRef(new Map());

    const remover = useCallback((id) => {
        clearTimeout(timers.current.get(id));
        timers.current.delete(id);
        setToasts((atual) => atual.filter((t) => t.id !== id));
    }, []);

    // Desmontar com timer pendente deixaria um setState em componente morto.
    useEffect(() => {
        const pendentes = timers.current;
        return () => {
            pendentes.forEach(clearTimeout);
            pendentes.clear();
        };
    }, []);

    /**
     * Enfileira um aviso, COLAPSANDO repeticao.
     *
     * Sem isto, uma tela com quatro consultas e a API fora do ar empilha quatro
     * vezes a mesma frase — o que faz a interface parecer quebrada justamente no
     * momento em que ela precisa parecer sob controle. Mensagem identica que
     * chega enquanto a anterior ainda esta na tela vira contador ("2x") e ganha
     * o tempo de leitura de novo.
     */
    const adicionar = useCallback(
        (message, type = "info") => {
            const id = ++seq.current;
            let reaproveitado = null;

            setToasts((atual) => {
                const existente = atual.find((t) => t.message === message && t.type === type);

                if (existente) {
                    reaproveitado = existente.id;
                    return atual.map((t) =>
                        t.id === existente.id ? { ...t, repeticoes: t.repeticoes + 1 } : t,
                    );
                }

                return [...atual, { id, message, type, repeticoes: 1 }];
            });

            const alvo = reaproveitado ?? id;

            // Reinicia a contagem: o timer antigo do toast reaproveitado apagaria
            // a mensagem antes de a repeticao ter sido lida.
            clearTimeout(timers.current.get(alvo));
            timers.current.set(
                alvo,
                setTimeout(() => remover(alvo), DURACAO_MS),
            );

            return alvo;
        },
        [remover],
    );

    // Ponte service -> UI. Um unico provider, uma unica assinatura.
    useEffect(() => toastBus.subscribe((message, type) => adicionar(message, type)), [adicionar]);

    const valor = useMemo(
        () => ({
            success: (m) => adicionar(m, "success"),
            error: (m) => adicionar(m, "error"),
            warning: (m) => adicionar(m, "warning"),
            info: (m) => adicionar(m, "info"),
            dismiss: remover,
        }),
        [adicionar, remover],
    );

    return (
        <ToastContext.Provider value={valor}>
            {children}
            <div className="toast toast-end toast-bottom z-toast max-w-[min(24rem,90vw)] p-4">
                {toasts.map((t) => (
                    <Toast
                        key={t.id}
                        type={t.type}
                        message={t.repeticoes > 1 ? `${t.message} (${t.repeticoes}x)` : t.message}
                        onClose={() => remover(t.id)}
                    />
                ))}
            </div>
        </ToastContext.Provider>
    );
}

export default ToastProvider;
