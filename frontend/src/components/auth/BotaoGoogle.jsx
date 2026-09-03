import { useState } from "react";
import { GoogleLogin } from "@react-oauth/google";
import { toastBus } from "@/api/toastBus.js";
import { googleHabilitado } from "@/lib/google.js";

/**
 * Entrada com Google (Google Identity Services).
 *
 * O componente devolve o `credential` — que e o id_token assinado pelo Google —
 * e nada mais. Quem valida a assinatura contra o JWKS do Google e o backend, em
 * POST /auth/google; o front nunca manda o proprio e-mail esperando que aceitem.
 *
 * O `GoogleOAuthProvider` NAO mora aqui: ele e montado uma unica vez na raiz
 * (`ProvedorGoogle` em main.jsx). Provider dentro do botao remontava o GSI a
 * cada tecla digitada no formulario — e a causa classica do widget piscando ou
 * simplesmente nao aparecendo.
 *
 * Sem `VITE_GOOGLE_CLIENT_ID` o bloco inteiro some, e a tela continua entrando
 * por e-mail e senha.
 *
 * `onErro` recebe a mensagem pronta para a tela: erro de sessao aparece ao lado
 * do formulario, nunca num toast flutuante no canto. O toast so entra quando
 * quem chamou nao passou `onErro` — melhor um aviso feio do que nenhum.
 */
export default function BotaoGoogle({
    onCredencial,
    onErro,
    rotulo = "signin_with",
    desabilitado = false,
}) {
    const [enviando, setEnviando] = useState(false);

    if (!googleHabilitado()) return null;

    function falhar(mensagem) {
        if (onErro) onErro(mensagem);
        else toastBus.emit(mensagem, "error");
    }

    async function receber(resposta) {
        const credencial = resposta?.credential;

        if (!credencial) {
            falhar("O Google não devolveu as credenciais. Tente novamente.");
            return;
        }

        setEnviando(true);
        try {
            await onCredencial?.(credencial);
        } finally {
            setEnviando(false);
        }
    }

    return (
        <div
            aria-busy={enviando || undefined}
            className={`flex justify-center ${
                enviando || desabilitado ? "pointer-events-none opacity-50" : ""
            }`}
        >
            <GoogleLogin
                onSuccess={receber}
                onError={() =>
                    falhar(
                        "Não foi possível entrar com o Google. Tente novamente ou use e-mail e senha.",
                    )
                }
                text={rotulo}
                locale="pt-BR"
                shape="rectangular"
                theme="outline"
                size="large"
                width="320"
            />
        </div>
    );
}
