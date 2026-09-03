import { useEffect, useState } from "react";
import { FiChevronLeft, FiChevronRight } from "react-icons/fi";

/**
 * Galeria da pagina de produto: foto grande 3:4 + miniaturas.
 *
 * As `midias` ja chegam filtradas pela cor escolhida (a page resolve o grupo em
 * `produto.galeria`). Quando a lista troca, a foto volta para a primeira — sem
 * isso, trocar de cor deixaria a pagina na terceira foto de outra cor.
 */
export default function GaleriaProduto({ midias = [], nome = "" }) {
    const [indice, setIndice] = useState(0);

    // Identidade da lista: a primeira foto + o tamanho bastam para detectar troca.
    const assinatura = `${midias[0]?.id ?? "vazio"}-${midias.length}`;

    useEffect(() => {
        setIndice(0);
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

    return (
        <div className="flex flex-col gap-4 lg:flex-row-reverse lg:items-start lg:gap-6">
            {/* -------------------------------------------------- FOTO PRINCIPAL */}
            <div className="relative w-full bg-linen lg:flex-1">
                <div className="aspect-product w-full">
                    <img
                        src={atual.url}
                        alt={atual.altText || nome}
                        loading="eager"
                        decoding="async"
                        className="h-full w-full object-cover"
                    />
                </div>

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
