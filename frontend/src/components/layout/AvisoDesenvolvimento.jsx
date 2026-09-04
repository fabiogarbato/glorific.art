import { useEffect, useState } from "react";
import { FiX } from "react-icons/fi";

const CHAVE_VISTO = "glorific_aviso_dev_visto";

/**
 * Aviso de "site em desenvolvimento", só na primeira visita (localStorage).
 * Reaproveita o selo giratório da página "em breve" que ficava no ar antes do
 * lançamento — é a assinatura visual que a marca já usava, então o aviso não
 * parece um elemento novo e desconectado do resto.
 */
export default function AvisoDesenvolvimento() {
    const [aberto, setAberto] = useState(false);

    useEffect(() => {
        try {
            if (!localStorage.getItem(CHAVE_VISTO)) setAberto(true);
        } catch {
            // Sem acesso a localStorage (modo privado etc.) — sem aviso, sem quebrar a loja.
        }
    }, []);

    const fechar = () => {
        setAberto(false);
        try {
            localStorage.setItem(CHAVE_VISTO, "1");
        } catch {
            /* idem — não trava o fechamento se não puder gravar. */
        }
    };

    if (!aberto) return null;

    return (
        <div className="fixed inset-0 z-top flex items-center justify-center bg-ink/50 p-4">
            <button
                type="button"
                aria-label="Fechar"
                tabIndex={-1}
                className="absolute inset-0 h-full w-full cursor-default"
                onClick={fechar}
            />

            <div
                role="dialog"
                aria-modal="true"
                aria-label="Site em desenvolvimento"
                className="relative w-full max-w-sm border border-sand bg-base-100 px-8 py-10 text-center shadow-[0_18px_60px_-24px_rgba(28,26,23,0.45)]"
            >
                <button
                    type="button"
                    onClick={fechar}
                    aria-label="Fechar"
                    className="absolute right-3 top-3 flex h-9 w-9 items-center justify-center text-ink-soft transition-colors hover:text-ink"
                >
                    <FiX size={16} />
                </button>

                <div className="relative mx-auto flex h-32 w-32 items-center justify-center">
                    <img
                        src="/logo-glorific.png"
                        alt="glorific.art"
                        className="relative z-10 w-24"
                    />

                    <svg
                        className="absolute inset-0 animate-girar"
                        viewBox="0 0 100 100"
                        aria-hidden="true"
                    >
                        <circle cx="50" cy="50" r="49" fill="none" stroke="#adacaa" strokeWidth="0.6" />
                        <circle cx="50" cy="50" r="43" fill="none" stroke="#adacaa" strokeWidth="0.6" />
                        <circle cx="50" cy="1" r="1.4" fill="#c8321f" />
                        <path
                            id="selo-caminho"
                            d="M 50,50 m -46,0 a 46,46 0 1,1 92,0 a 46,46 0 1,1 -92,0"
                            fill="none"
                        />
                        <text fontSize="4.3" fontWeight="700" letterSpacing="0.22em" fill="#adacaa">
                            <textPath href="#selo-caminho" startOffset="0%">
                                GLORIFIC · EM CONSTRUÇÃO · GLORIFIC · EM CONSTRUÇÃO ·
                            </textPath>
                        </text>
                    </svg>
                </div>

                <p className="mt-6 font-display text-xl tracking-tight text-ink">
                    Em construção.
                </p>
                <p className="mt-3 text-sm leading-relaxed text-ink-soft">
                    A Glorific está no ar, mas ainda em desenvolvimento. Algumas coisas por
                    aqui vão mudar. Fique à vontade pra olhar a coleção.
                </p>

                <button
                    type="button"
                    onClick={fechar}
                    className="mt-8 inline-flex h-11 w-full items-center justify-center bg-olive px-6 font-sans text-xs uppercase tracking-widest text-bone transition-colors hover:bg-olive-dp"
                >
                    Entendi
                </button>
            </div>
        </div>
    );
}
