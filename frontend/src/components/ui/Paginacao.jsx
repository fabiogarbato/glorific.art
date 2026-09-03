import { FiChevronLeft, FiChevronRight } from "react-icons/fi";

/**
 * Paginacao com elipse: mostra sempre a primeira, a ultima e a vizinhanca da
 * pagina atual. Devolve `['1', '...', 4, 5, 6, '...', 12]`.
 */
export function montarPaginas(paginaAtual, totalPaginas, vizinhanca = 1) {
    return Array.from({ length: totalPaginas }, (_, i) => i + 1)
        .filter(
            (p) => p === 1 || p === totalPaginas || Math.abs(p - paginaAtual) <= vizinhanca,
        )
        .reduce((acc, p, i, arr) => {
            if (i > 0 && p - arr[i - 1] > 1) acc.push("...");
            acc.push(p);
            return acc;
        }, []);
}

const BTN =
    "inline-flex h-9 min-w-[2.25rem] items-center justify-center border border-sand bg-base-100 px-2 " +
    "font-sans text-xs text-ink transition-colors hover:bg-linen disabled:opacity-35 " +
    "disabled:hover:bg-base-100 disabled:cursor-not-allowed";

export default function Paginacao({
    paginaAtual = 1,
    totalPaginas = 1,
    onMudarPagina,
    totalItens,
    itensPorPagina,
    className = "",
}) {
    if (totalPaginas <= 1) return null;

    const paginas = montarPaginas(paginaAtual, totalPaginas);
    const inicio = (paginaAtual - 1) * (itensPorPagina ?? 0) + 1;
    const fim = Math.min(paginaAtual * (itensPorPagina ?? 0), totalItens ?? 0);

    return (
        <nav
            aria-label="Paginação"
            className={`flex flex-wrap items-center justify-between gap-3 ${className}`}
        >
            {totalItens != null && itensPorPagina != null && (
                <p className="text-xs text-ink-soft">
                    {inicio}–{fim} de {totalItens}
                </p>
            )}

            <div className="flex items-center gap-1">
                <button
                    type="button"
                    className={BTN}
                    aria-label="Página anterior"
                    disabled={paginaAtual === 1}
                    onClick={() => onMudarPagina(paginaAtual - 1)}
                >
                    <FiChevronLeft size={15} />
                </button>

                {paginas.map((p, i) =>
                    p === "..." ? (
                        <span
                            key={`gap-${i}`}
                            className="px-1 text-xs text-taupe"
                            aria-hidden="true"
                        >
                            …
                        </span>
                    ) : (
                        <button
                            key={p}
                            type="button"
                            aria-current={p === paginaAtual ? "page" : undefined}
                            className={`${BTN} ${
                                p === paginaAtual
                                    ? "border-olive bg-olive text-bone hover:bg-olive-dp"
                                    : ""
                            }`}
                            onClick={() => onMudarPagina(p)}
                        >
                            {p}
                        </button>
                    ),
                )}

                <button
                    type="button"
                    className={BTN}
                    aria-label="Próxima página"
                    disabled={paginaAtual === totalPaginas}
                    onClick={() => onMudarPagina(paginaAtual + 1)}
                >
                    <FiChevronRight size={15} />
                </button>
            </div>
        </nav>
    );
}
