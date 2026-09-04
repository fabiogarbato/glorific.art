import { Link } from "react-router-dom";

/**
 * Cartão de indicador do painel.
 *
 * Sem sombra e sem canto arredondado: no editorial o que separa e o filete e o
 * respiro. O número usa `font-display` (Cormorant) com `tabular-nums` para as
 * colunas não dançarem quando o valor muda.
 */
export default function CartaoMetrica({
    rotulo,
    valor,
    apoio,
    Icone,
    tom = "neutro",
    onClick,
    to,
}) {
    const cor =
        tom === "alerta"
            ? "text-warning"
            : tom === "critico"
              ? "text-danger"
              : tom === "positivo"
                ? "text-olive"
                : "text-ink-soft";

    const conteudo = (
        <>
            <div className="flex items-center gap-2">
                {Icone && <Icone size={14} className={cor} aria-hidden="true" />}
                <span className="text-xs uppercase tracking-widest text-ink-soft">{rotulo}</span>
            </div>
            <p className="preco mt-3 font-display text-xl leading-tight text-ink">{valor}</p>
            {apoio && <p className="mt-1 text-xs leading-relaxed text-ink-soft">{apoio}</p>}
        </>
    );

    const classe =
        "block w-full border border-sand bg-base-100 px-4 py-5 text-left shadow-sm transition-colors";

    if (to) {
        return (
            <Link to={to} className={`${classe} hover:bg-sand/40`}>
                {conteudo}
            </Link>
        );
    }

    if (onClick) {
        return (
            <button type="button" onClick={onClick} className={`${classe} hover:bg-sand/40`}>
                {conteudo}
            </button>
        );
    }

    return <article className={classe}>{conteudo}</article>;
}
