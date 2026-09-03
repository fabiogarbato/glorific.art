import { useState } from "react";
import { FiX } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";

/**
 * Cupom do carrinho.
 *
 * O que aparece aqui é PRÉVIA: quem valida de verdade é o checkout, que
 * recalcula tudo no servidor. Por isso o componente nunca calcula desconto — ele
 * só manda o código e mostra o que o backend respondeu.
 *
 * `avisoCupom` chega quando o cupom aplicado deixou de valer (venceu, esgotou,
 * não atinge o valor mínimo). Some avisar do que deixar a pessoa descobrir o
 * total cheio na hora de pagar.
 */
export default function CampoCupom({
    codigoAplicado,
    aviso,
    onAplicar,
    onRemover,
    salvando = false,
}) {
    const [codigo, setCodigo] = useState("");
    const [erro, setErro] = useState("");

    async function submeter(evento) {
        evento.preventDefault();

        const limpo = codigo.trim().toUpperCase();
        if (limpo.length < 2) {
            setErro("Informe um código com pelo menos 2 caracteres.");
            return;
        }

        setErro("");
        try {
            await onAplicar(limpo);
            setCodigo("");
        } catch {
            // O interceptor do axios já mostrou o motivo em toast. Repetir aqui
            // faria a pessoa ler a mesma frase duas vezes.
        }
    }

    if (codigoAplicado) {
        return (
            <div className="flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3 border border-olive bg-linen px-3 py-2.5">
                    <p className="min-w-0 text-sm text-ink">
                        <span className="eyebrow mr-2">Cupom</span>
                        <span className="font-sans uppercase tracking-widest">
                            {codigoAplicado}
                        </span>
                    </p>

                    <button
                        type="button"
                        onClick={onRemover}
                        disabled={salvando}
                        aria-label={`Remover o cupom ${codigoAplicado}`}
                        className="flex h-9 w-9 shrink-0 items-center justify-center text-ink-soft transition-colors hover:text-danger disabled:opacity-40"
                    >
                        <FiX size={16} />
                    </button>
                </div>

                {aviso && (
                    <p role="status" className="text-xs text-danger">
                        {aviso}
                    </p>
                )}
            </div>
        );
    }

    return (
        <form onSubmit={submeter} className="flex flex-col gap-1.5">
            <label htmlFor="campo-cupom" className="eyebrow">
                Cupom de desconto
            </label>

            <div className="flex gap-2">
                <input
                    id="campo-cupom"
                    value={codigo}
                    onChange={(e) => setCodigo(e.target.value.toUpperCase())}
                    maxLength={50}
                    autoComplete="off"
                    placeholder="SEUCUPOM"
                    aria-invalid={erro ? true : undefined}
                    aria-describedby={erro ? "campo-cupom-erro" : undefined}
                    className={`h-11 w-full border bg-base-100 px-3 font-sans text-sm uppercase tracking-widest text-ink placeholder:normal-case placeholder:tracking-normal placeholder:text-taupe focus:outline-none ${
                        erro ? "border-danger" : "border-sand focus:border-olive"
                    }`}
                />

                <Botao type="submit" variante="contorno" carregando={salvando}>
                    Aplicar
                </Botao>
            </div>

            {(erro || aviso) && (
                <p id="campo-cupom-erro" role="alert" className="text-xs text-danger">
                    {erro || aviso}
                </p>
            )}
        </form>
    );
}
