import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import Botao from "@/components/ui/Botao.jsx";
import CampoSenha from "@/components/auth/CampoSenha.jsx";
import MolduraAuth from "@/components/auth/MolduraAuth.jsx";
import { useAuth } from "@/hooks/useAuth.js";
import { useToast } from "@/hooks/useToast.js";
import { SENHA } from "@/lib/constants.js";
import { getApiError } from "@/utils/apiError.js";

/**
 * Redefinicao pelo link do e-mail (`/redefinir-senha?token=...`, montado pelo
 * backend em AutenticacaoService).
 *
 * O token fica so na URL e no corpo da requisicao — nunca em storage. Ele vale
 * uma vez, e redefinir derruba TODAS as sessoes abertas, entao a tela manda a
 * pessoa para o login em vez de fingir que ela continua conectada.
 */
export default function RedefinirSenha() {
    const [params] = useSearchParams();
    const token = params.get("token") ?? "";

    const [form, setForm] = useState({ senha: "", confirmacao: "" });
    const [erros, setErros] = useState({});
    const [erroGeral, setErroGeral] = useState("");
    const [enviando, setEnviando] = useState(false);

    const { redefinirSenha } = useAuth();
    const toast = useToast();
    const navigate = useNavigate();

    // Link truncado pelo cliente de e-mail, ou colado pela metade. Sem token nao
    // ha o que enviar: melhor dizer isso do que deixar a pessoa digitar a senha a toa.
    if (!token) {
        return (
            <MolduraAuth
                rotulo="Área do cliente"
                titulo="Link inválido"
                descricao="Este endereço não traz o código de redefinição. Ele pode ter sido cortado pelo seu programa de e-mail."
                rodape={
                    <Link
                        to="/esqueci-senha"
                        className="text-ink underline decoration-sand underline-offset-4 hover:decoration-ink"
                    >
                        Pedir um link novo
                    </Link>
                }
            />
        );
    }

    function alterar(e) {
        const { name, value } = e.target;
        setForm((f) => ({ ...f, [name]: value }));
        setErros((atual) => ({ ...atual, [name]: undefined }));
    }

    function validar() {
        const novos = {};
        if (form.senha.length < SENHA.MIN) {
            novos.senha = `A senha precisa de ao menos ${SENHA.MIN} caracteres.`;
        }
        if (form.confirmacao !== form.senha) {
            novos.confirmacao = "As senhas não são iguais.";
        }
        setErros(novos);
        return Object.keys(novos).length === 0;
    }

    async function submeter(e) {
        e.preventDefault();
        setErroGeral("");
        if (!validar()) return;

        setEnviando(true);
        try {
            await redefinirSenha({ token, novaSenha: form.senha });
            toast.success("Senha alterada. Entre com a senha nova.");
            navigate("/login", { replace: true });
        } catch (err) {
            setErroGeral(
                getApiError(err).message ||
                    "Não foi possível redefinir a senha. Peça um link novo e tente de novo.",
            );
        } finally {
            setEnviando(false);
        }
    }

    return (
        <MolduraAuth
            rotulo="Área do cliente"
            titulo="Criar senha nova"
            descricao="Escolha uma senha que você não use em outro site. Ao confirmar, todas as sessões abertas são encerradas."
            erro={erroGeral}
            rodape={
                <Link
                    to="/esqueci-senha"
                    className="text-ink-soft underline decoration-sand underline-offset-4 transition-colors hover:text-ink"
                >
                    Pedir outro link
                </Link>
            }
        >
            <form onSubmit={submeter} noValidate className="mt-10 flex flex-col gap-6">
                <CampoSenha
                    label="Nova senha"
                    name="senha"
                    autoComplete="new-password"
                    maxLength={SENHA.MAX}
                    value={form.senha}
                    onChange={alterar}
                    erro={erros.senha}
                    ajuda={`De ${SENHA.MIN} a ${SENHA.MAX} caracteres.`}
                    obrigatorio
                />

                <CampoSenha
                    label="Repetir a nova senha"
                    name="confirmacao"
                    autoComplete="new-password"
                    maxLength={SENHA.MAX}
                    value={form.confirmacao}
                    onChange={alterar}
                    erro={erros.confirmacao}
                    obrigatorio
                />

                <Botao type="submit" blocoCompleto carregando={enviando}>
                    Salvar senha
                </Botao>
            </form>
        </MolduraAuth>
    );
}
