import { useId, useState } from "react";
import Campo from "@/components/ui/Campo.jsx";
import { SENHA_MAX } from "@/utils/masks.js";

/**
 * Campo de senha com alternador de visibilidade.
 *
 * O botao fica ABAIXO do campo, e nao sobreposto: sem posicionamento absoluto
 * nao ha como ele cobrir o texto digitado em tela estreita, nem escorregar
 * quando a mensagem de erro aparece. Ele carrega `aria-pressed`, entao o leitor
 * de tela anuncia o estado, e nao so o rotulo.
 */
export default function CampoSenha({
    label = "Senha",
    ajuda,
    erro,
    id,
    maxLength = SENHA_MAX,
    ...props
}) {
    const [visivel, setVisivel] = useState(false);
    const gerado = useId();
    const campoId = id ?? gerado;

    return (
        <div className="flex flex-col gap-2">
            <Campo
                id={campoId}
                label={label}
                type={visivel ? "text" : "password"}
                maxLength={maxLength}
                ajuda={ajuda}
                erro={erro}
                {...props}
            />

            <button
                type="button"
                onClick={() => setVisivel((v) => !v)}
                aria-pressed={visivel}
                aria-controls={campoId}
                className="self-start font-sans text-xs text-ink-soft underline decoration-sand underline-offset-4 transition-colors hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-2"
            >
                {visivel ? "Ocultar senha" : "Mostrar senha"}
            </button>
        </div>
    );
}
