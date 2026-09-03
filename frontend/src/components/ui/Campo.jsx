import { forwardRef, useId } from "react";

/**
 * Campo de formulario editorial: sem caixa arredondada, filete `sand` embaixo
 * quando `variante="linha"`. Suporta `<input>`, `<textarea>` e `<select>` pelo
 * mesmo contrato — o form fica com uma unica gramatica.
 */
const Campo = forwardRef(function Campo(
    {
        label,
        erro,
        ajuda,
        como = "input",
        variante = "caixa",
        className = "",
        containerClassName = "",
        id,
        obrigatorio = false,
        children,
        ...props
    },
    ref,
) {
    const gerado = useId();
    const campoId = id ?? gerado;
    const ajudaId = `${campoId}-ajuda`;

    const base =
        "w-full bg-transparent font-sans text-base text-ink placeholder:text-taupe " +
        "transition-colors duration-200 focus:outline-none disabled:opacity-50";

    const skin =
        variante === "linha"
            ? "border-0 border-b border-sand px-0 py-2 focus:border-olive"
            : "border border-sand bg-base-100 px-3 py-2.5 focus:border-olive";

    const classes = [base, skin, erro ? "border-danger focus:border-danger" : "", className]
        .filter(Boolean)
        .join(" ");

    const comuns = {
        id: campoId,
        ref,
        className: classes,
        "aria-invalid": erro ? true : undefined,
        "aria-describedby": erro || ajuda ? ajudaId : undefined,
        ...props,
    };

    return (
        <div className={["flex flex-col gap-1.5", containerClassName].filter(Boolean).join(" ")}>
            {label && (
                <label htmlFor={campoId} className="eyebrow">
                    {label}
                    {obrigatorio && <span className="ml-1 text-danger">*</span>}
                </label>
            )}

            {como === "textarea" && <textarea rows={4} {...comuns} />}
            {como === "select" && <select {...comuns}>{children}</select>}
            {como === "input" && <input {...comuns} />}

            {(erro || ajuda) && (
                <p
                    id={ajudaId}
                    className={`text-xs ${erro ? "text-danger" : "text-ink-soft"}`}
                    role={erro ? "alert" : undefined}
                >
                    {erro || ajuda}
                </p>
            )}
        </div>
    );
});

export default Campo;
