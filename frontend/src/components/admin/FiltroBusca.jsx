import { useEffect, useId, useState } from "react";
import { FiSearch, FiX } from "react-icons/fi";
import { ITENS_POR_PAGINA_OPTIONS } from "@/lib/constants.js";

/**
 * Faixa de filtros das listagens do painel.
 *
 * A busca é debounced (350 ms) para não disparar uma requisição por tecla. Quem
 * chama passa `onBuscar`, e o hook `useListaAdmin` do lado da página é quem
 * zera a paginação — trocar filtro estando na página 4 devolveria uma lista
 * vazia que parece "não encontrou nada".
 *
 * `escopo` deixa explícito para quem usa a tela onde a busca acontece:
 * "servidor" quando a rota aceita o termo (é o caso de produtos) e "local"
 * quando o recurso herda o CRUD genérico, que só aceita `page` e `pageSize`.
 */
const ATRASO_MS = 350;

export default function FiltroBusca({
    valor = "",
    onBuscar,
    rotulo = "Buscar",
    placeholder = "Buscar...",
    escopo = "servidor",
    tamanhoPagina,
    onTamanhoPagina,
    children,
    onLimpar,
    className = "",
}) {
    const idCampo = useId();
    const idTamanho = `${idCampo}-tamanho`;
    const [texto, setTexto] = useState(valor);

    // Sincroniza quando o pai limpa os filtros por fora.
    useEffect(() => {
        setTexto(valor);
    }, [valor]);

    useEffect(() => {
        if (texto === valor) return undefined;
        const timer = setTimeout(() => onBuscar?.(texto), ATRASO_MS);
        return () => clearTimeout(timer);
    }, [texto, valor, onBuscar]);

    const temFiltro = !!texto;

    const ajuda =
        escopo === "local"
            ? "Esta lista da API aceita apenas paginação. A busca filtra os itens da página aberta."
            : null;

    return (
        <section aria-label="Filtros" className={`mb-6 ${className}`}>
            <div className="flex flex-col gap-3 border border-sand bg-linen/50 p-4 lg:flex-row lg:items-end">
                <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                    <label htmlFor={idCampo} className="eyebrow">
                        {rotulo}
                    </label>
                    <div className="relative">
                        <FiSearch
                            size={15}
                            aria-hidden="true"
                            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-taupe"
                        />
                        <input
                            id={idCampo}
                            type="search"
                            value={texto}
                            placeholder={placeholder}
                            onChange={(e) => setTexto(e.target.value)}
                            className="w-full border border-sand bg-base-100 py-2.5 pl-9 pr-9 font-sans text-base text-ink placeholder:text-taupe transition-colors focus:border-olive focus:outline-none"
                        />
                        {temFiltro && (
                            <button
                                type="button"
                                aria-label="Limpar busca"
                                onClick={() => setTexto("")}
                                className="absolute right-2 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center text-taupe transition-colors hover:text-ink"
                            >
                                <FiX size={15} />
                            </button>
                        )}
                    </div>
                </div>

                {children}

                {onTamanhoPagina && (
                    <div className="flex w-full flex-col gap-1.5 lg:w-40">
                        <label htmlFor={idTamanho} className="eyebrow">
                            Itens por página
                        </label>
                        <select
                            id={idTamanho}
                            value={tamanhoPagina}
                            onChange={(e) => onTamanhoPagina(Number(e.target.value))}
                            className="w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-base text-ink transition-colors focus:border-olive focus:outline-none"
                        >
                            {ITENS_POR_PAGINA_OPTIONS.map((n) => (
                                <option key={n} value={n}>
                                    {n}
                                </option>
                            ))}
                        </select>
                    </div>
                )}

                {onLimpar && (
                    <button
                        type="button"
                        onClick={() => {
                            setTexto("");
                            onLimpar();
                        }}
                        className="h-11 shrink-0 border border-ink px-5 font-sans text-xs uppercase tracking-widest text-ink transition-colors hover:bg-ink hover:text-bone"
                    >
                        Limpar
                    </button>
                )}
            </div>

            {ajuda && <p className="mt-2 text-xs text-taupe">{ajuda}</p>}
        </section>
    );
}
