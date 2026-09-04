import { useEffect, useRef, useState } from "react";
import { FiChevronLeft, FiChevronRight } from "react-icons/fi";

const TAMANHO_LUPA = 170;
const FATOR_ZOOM = 2.4;

/**
 * Galeria da pagina de produto: foto grande 3:4 + miniaturas + lupa no hover
 * (desktop: um quadrado que segue o cursor e mostra a area sob ele ampliada).
 *
 * As `midias` ja chegam filtradas pela cor escolhida (a page resolve o grupo em
 * `produto.galeria`). Quando a lista troca, a foto volta para a primeira — sem
 * isso, trocar de cor deixaria a pagina na terceira foto de outra cor.
 */
export default function GaleriaProduto({ midias = [], nome = "" }) {
    const [indice, setIndice] = useState(0);
    const [lupa, setLupa] = useState(null); // { left, top, bgSize, bgPosX, bgPosY } | null
    const imagemRef = useRef(null);

    // Identidade da lista: a primeira foto + o tamanho bastam para detectar troca.
    const assinatura = `${midias[0]?.id ?? "vazio"}-${midias.length}`;

    useEffect(() => {
        setIndice(0);
        setLupa(null);
    }, [assinatura]);

    if (!midias.length) {
        return (
            <div
                className="aspect-product w-full bg-gradient-to-b from-sand via-linen to-bone"
                role="img"
                aria-label={`Sem foto disponível de ${nome}`}
            />
        );
    }

    const atual = midias[Math.min(indice, midias.length - 1)];
    const temVarias = midias.length > 1;

    const irPara = (proximo) => {
        const total = midias.length;
        setIndice(((proximo % total) + total) % total);
    };

    // A lupa so faz sentido em tela com mouse de verdade — em telas menores que
    // o breakpoint lg o hover nao existe, entao nem calculamos.
    const moverLupa = (evento) => {
        const box = imagemRef.current?.getBoundingClientRect();
        if (!box) return;

        const cursorX = Math.min(box.width, Math.max(0, evento.clientX - box.left));
        const cursorY = Math.min(box.height, Math.max(0, evento.clientY - box.top));

        const left = Math.min(
            Math.max(0, cursorX - TAMANHO_LUPA / 2),
            box.width - TAMANHO_LUPA,
        );
        const top = Math.min(
            Math.max(0, cursorY - TAMANHO_LUPA / 2),
            box.height - TAMANHO_LUPA,
        );

        setLupa({
            left,
            top,
            bgSize: `${box.width * FATOR_ZOOM}px ${box.height * FATOR_ZOOM}px`,
            bgPosX: `${-left * FATOR_ZOOM}px`,
            bgPosY: `${-top * FATOR_ZOOM}px`,
        });
    };

    return (
        <div className="flex flex-col gap-4 lg:flex-row-reverse lg:items-start lg:gap-6">
            {/* -------------------------------------------------- FOTO PRINCIPAL */}
            <div
                ref={imagemRef}
                onMouseMove={moverLupa}
                onMouseLeave={() => setLupa(null)}
                className="relative w-full bg-linen lg:flex-1 lg:h-[min(58vh,480px)] lg:cursor-zoom-in"
            >
                <div className="aspect-product w-full lg:h-full lg:w-auto lg:max-w-full lg:mx-auto">
                    <img
                        src={atual.url}
                        alt={atual.altText || nome}
                        loading="eager"
                        decoding="async"
                        className="h-full w-full object-cover"
                    />
                </div>

                {/* Lupa: quadrado que segue o cursor e mostra a mesma foto ampliada,
                    recortada exatamente na area sob o cursor. */}
                {lupa && (
                    <div
                        aria-hidden="true"
                        className="pointer-events-none absolute hidden border-2 border-base-100 shadow-lg lg:block"
                        style={{
                            left: lupa.left,
                            top: lupa.top,
                            width: TAMANHO_LUPA,
                            height: TAMANHO_LUPA,
                            backgroundImage: `url(${atual.url})`,
                            backgroundRepeat: "no-repeat",
                            backgroundSize: lupa.bgSize,
                            backgroundPosition: `${lupa.bgPosX} ${lupa.bgPosY}`,
                        }}
                    />
                )}

                {temVarias && (
                    <>
                        <button
                            type="button"
                            aria-label="Foto anterior"
                            onClick={() => irPara(indice - 1)}
                            className="absolute left-3 top-1/2 flex h-11 w-11 -translate-y-1/2 items-center justify-center bg-base-100/85 text-ink transition-colors hover:bg-base-100"
                        >
                            <FiChevronLeft size={18} aria-hidden="true" />
                        </button>
                        <button
                            type="button"
                            aria-label="Próxima foto"
                            onClick={() => irPara(indice + 1)}
                            className="absolute right-3 top-1/2 flex h-11 w-11 -translate-y-1/2 items-center justify-center bg-base-100/85 text-ink transition-colors hover:bg-base-100"
                        >
                            <FiChevronRight size={18} aria-hidden="true" />
                        </button>

                        <p className="absolute bottom-3 right-3 bg-base-100/85 px-2 py-1 text-xs tabular-nums text-ink-soft">
                            {indice + 1} / {midias.length}
                        </p>
                    </>
                )}
            </div>

            {/* ----------------------------------------------------- MINIATURAS */}
            {temVarias && (
                <div
                    role="group"
                    aria-label="Miniaturas do produto"
                    className="flex gap-3 overflow-x-auto pb-1 lg:w-20 lg:shrink-0 lg:flex-col lg:overflow-visible lg:pb-0"
                >
                    {midias.map((midia, i) => (
                        <button
                            key={midia.id}
                            type="button"
                            onClick={() => setIndice(i)}
                            aria-label={`Ver foto ${i + 1} de ${midias.length}`}
                            aria-pressed={i === indice}
                            className={`aspect-product w-16 shrink-0 overflow-hidden border transition-colors lg:w-full ${
                                i === indice
                                    ? "border-ink"
                                    : "border-transparent hover:border-sand"
                            }`}
                        >
                            <img
                                src={midia.url}
                                alt=""
                                loading="lazy"
                                decoding="async"
                                className="h-full w-full object-cover"
                            />
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
}
