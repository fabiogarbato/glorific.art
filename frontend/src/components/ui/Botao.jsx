import { forwardRef } from "react";
import { Link } from "react-router-dom";

/**
 * Botao do design-system: retangular (`rounded-none`), caixa alta, `text-xs`
 * com `tracking-widest`. O primario e `olive` sobre `bone`.
 *
 * Renderiza <button>, <a> (prop `href`) ou <Link> (prop `to`) mantendo o visual.
 */
const VARIANTES = {
    primario: "bg-olive text-bone hover:bg-olive-dp active:bg-olive-dp border border-olive hover:border-olive-dp",
    contorno: "bg-transparent text-ink border border-ink hover:bg-ink hover:text-bone",
    sutil: "bg-linen text-ink border border-transparent hover:bg-sand",
    acento: "bg-brass text-ink border border-brass hover:bg-[#9c7a48] hover:border-[#9c7a48]",
    perigo: "bg-danger text-bone border border-danger hover:brightness-95",
    texto: "bg-transparent text-ink-soft border border-transparent hover:text-ink underline underline-offset-4 decoration-sand hover:decoration-ink",
};

const TAMANHOS = {
    sm: "h-9 px-4 text-[11px]",
    md: "h-11 px-6 text-xs",
    lg: "h-14 px-10 text-xs",
};

const BASE =
    "inline-flex items-center justify-center gap-2 rounded-none font-sans uppercase tracking-widest " +
    "transition-colors duration-200 disabled:opacity-40 disabled:cursor-not-allowed " +
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-2 " +
    "focus-visible:ring-offset-base-100";

const Botao = forwardRef(function Botao(
    {
        variante = "primario",
        tamanho = "md",
        carregando = false,
        blocoCompleto = false,
        className = "",
        children,
        to,
        href,
        type = "button",
        disabled,
        ...props
    },
    ref,
) {
    const classes = [
        BASE,
        VARIANTES[variante] ?? VARIANTES.primario,
        TAMANHOS[tamanho] ?? TAMANHOS.md,
        blocoCompleto ? "w-full" : "",
        className,
    ]
        .filter(Boolean)
        .join(" ");

    const conteudo = (
        <>
            {carregando && <span className="loading loading-spinner loading-xs" aria-hidden="true" />}
            {children}
        </>
    );

    if (to) {
        return (
            <Link ref={ref} to={to} className={classes} {...props}>
                {conteudo}
            </Link>
        );
    }

    if (href) {
        return (
            <a ref={ref} href={href} className={classes} {...props}>
                {conteudo}
            </a>
        );
    }

    return (
        <button
            ref={ref}
            type={type}
            className={classes}
            disabled={disabled || carregando}
            aria-busy={carregando || undefined}
            {...props}
        >
            {conteudo}
        </button>
    );
});

export default Botao;
