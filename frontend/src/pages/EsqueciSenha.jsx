import { useState } from "react";
import { Link } from "react-router-dom";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import MolduraAuth from "@/components/auth/MolduraAuth.jsx";
import { useAuth } from "@/hooks/useAuth.js";
import { getApiError } from "@/utils/apiError.js";
import { EMAIL_MAXLENGTH, isValidEmail, normalizeEmail } from "@/utils/masks.js";

/**
 * Pedido do link de redefinicao.
 *
 * O backend responde 204 exista a conta ou nao, e esta tela precisa fazer o
 * mesmo: se o texto mudasse conforme o e-mail existir, qualquer um poderia usar
 * este formulario para descobrir quem tem conta na loja. Por isso a confirmacao
 * fala em "se existir uma conta", e nunca "enviamos para voce".
 */
export default function EsqueciSenha() {
    const [email, setEmail] = useState("");
    const [erro, setErro] = useState("");
    const [erroGeral, setErroGeral] = useState("");
    const [enviando, setEnviando] = useState(false);
    const [enviado, setEnviado] = useState(false);

    const { esqueciSenha } = useAuth();

    async function submeter(e) {
        e.preventDefault();
        setErroGeral("");

        if (!isValidEmail(email)) {
            setErro("Informe um e-mail válido.");
            return;
        }
        setErro("");

        setEnviando(true);
        try {
            await esqueciSenha(normalizeEmail(email));
            setEnviado(true);
        } catch (err) {
            setErroGeral(
                getApiError(err).message ||
                    "Não foi possível enviar o link agora. Tente novamente em instantes.",
            );
        } finally {
            setEnviando(false);
        }
    }

    const voltar = (
        <p className="text-ink-soft">
            Lembrou a senha?{" "}
            <Link
                to="/login"
                className="text-ink underline decoration-sand underline-offset-4 hover:decoration-ink"
            >
                Entrar
            </Link>
        </p>
    );

    if (enviado) {
        return (
            <MolduraAuth
                rotulo="Área do cliente"
                titulo="Verifique seu e-mail"
                descricao={`Se existir uma conta para ${normalizeEmail(email)}, o link de redefinição está a caminho. Ele vale por tempo limitado e só pode ser usado uma vez.`}
                rodape={
                    <>
                        <button
                            type="button"
                            onClick={() => setEnviado(false)}
                            className="self-start text-ink-soft underline decoration-sand underline-offset-4 transition-colors hover:text-ink"
                        >
                            Enviar para outro e-mail
                        </button>
                        {voltar}
                    </>
                }
            >
                <p className="mt-6 text-sm leading-relaxed text-ink-soft">
                    Não encontrou? Confira a caixa de spam antes de pedir um novo link.
                </p>
            </MolduraAuth>
        );
    }

    return (
        <MolduraAuth
            rotulo="Área do cliente"
            titulo="Recuperar senha"
            descricao="Informe o e-mail do seu cadastro e enviaremos um link para criar uma senha nova."
            erro={erroGeral}
            rodape={voltar}
        >
            <form onSubmit={submeter} noValidate className="mt-10 flex flex-col gap-6">
                <Campo
                    label="E-mail"
                    name="email"
                    type="email"
                    autoComplete="email"
                    maxLength={EMAIL_MAXLENGTH}
                    value={email}
                    onChange={(e) => {
                        setEmail(e.target.value);
                        setErro("");
                    }}
                    onBlur={() => setEmail((v) => normalizeEmail(v))}
                    erro={erro}
                    obrigatorio
                />

                <Botao type="submit" blocoCompleto carregando={enviando}>
                    Enviar link
                </Botao>
            </form>
        </MolduraAuth>
    );
}
