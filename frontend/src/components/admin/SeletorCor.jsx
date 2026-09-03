import { useId } from "react";
import { FiCheck } from "react-icons/fi";

/**
 * Swatches de cor.
 *
 * A bolinha é pintada com o `hexRgb` que veio da API (o backend só aceita
 * `#RRGGBB`, então o valor é sempre pintável). Estampa não tem cor chapada que
 * a represente: quando a cor tem `urlMidiaSwatch`, é a imagem que aparece.
 *
 * Acessibilidade: cor sozinha não é informação. Cada swatch é um botão com
 * `aria-pressed` e o nome da cor legível, e o nome também aparece embaixo.
 */
export function Swatch({ cor, tamanho = 28, className = "" }) {
    const estilo = cor?.urlMidiaSwatch
        ? {
              backgroundImage: `url(${cor.urlMidiaSwatch})`,
              backgroundSize: "cover",
              backgroundPosition: "center",
              width: tamanho,
              height: tamanho,
          }
        : { backgroundColor: cor?.hexRgb || "transparent", width: tamanho, height: tamanho };

    return (
        <span
            aria-hidden="true"
            style={estilo}
            className={`inline-block shrink-0 border border-sand ${className}`}
        />
    );
}

/**
 * `multiplo = false`: `valor` (id) + `onChange(id | null)`.
 * `multiplo = true`:  `valores` (array de ids) + `onAlternar(id)`.
 */
export default function SeletorCor({
    label,
    cores = [],
    valor = null,
    valores = [],
    onChange,
    onAlternar,
    multiplo = false,
    permitirNenhuma = false,
    rotuloNenhuma = "Sem cor definida",
    erro,
    ajuda,
    obrigatorio = false,
    className = "",
}) {
    const grupoId = useId();
    const ajudaId = `${grupoId}-ajuda`;

    const estaSelecionada = (id) => (multiplo ? valores.includes(id) : valor === id);

    const alternar = (id) => {
        if (multiplo) onAlternar?.(id);
        else onChange?.(valor === id && permitirNenhuma ? null : id);
    };

    return (
        <fieldset
            className={`flex flex-col gap-2 ${className}`}
            aria-describedby={erro || ajuda ? ajudaId : undefined}
        >
            {label && (
                <legend className="eyebrow mb-1">
                    {label}
                    {obrigatorio && <span className="ml-1 text-danger">*</span>}
                </legend>
            )}

            {cores.length === 0 ? (
                <p className="text-sm text-ink-soft">
                    Nenhuma cor ativa cadastrada. Cadastre as cores antes de montar a grade.
                </p>
            ) : (
                <div className="flex flex-wrap gap-2">
                    {permitirNenhuma && !multiplo && (
                        <button
                            type="button"
                            aria-pressed={valor === null}
                            onClick={() => onChange?.(null)}
                            className={`flex h-11 items-center gap-2 border px-3 font-sans text-xs transition-colors ${
                                valor === null
                                    ? "border-olive bg-olive/10 text-ink"
                                    : "border-sand bg-base-100 text-ink-soft hover:border-ink"
                            }`}
                        >
                            {rotuloNenhuma}
                        </button>
                    )}

                    {cores.map((cor) => {
                        const marcada = estaSelecionada(cor.id);
                        return (
                            <button
                                key={cor.id}
                                type="button"
                                aria-pressed={marcada}
                                onClick={() => alternar(cor.id)}
                                title={cor.nome}
                                className={`flex h-11 items-center gap-2 border px-3 font-sans text-xs transition-colors ${
                                    marcada
                                        ? "border-olive bg-olive/10 text-ink"
                                        : "border-sand bg-base-100 text-ink-soft hover:border-ink"
                                }`}
                            >
                                <Swatch cor={cor} tamanho={18} />
                                <span className="max-w-[10rem] truncate">{cor.nome}</span>
                                {marcada && (
                                    <FiCheck size={13} className="text-olive" aria-hidden="true" />
                                )}
                            </button>
                        );
                    })}
                </div>
            )}

            {(erro || ajuda) && (
                <p
                    id={ajudaId}
                    className={`text-xs ${erro ? "text-danger" : "text-ink-soft"}`}
                    role={erro ? "alert" : undefined}
                >
                    {erro || ajuda}
                </p>
            )}
        </fieldset>
    );
}
