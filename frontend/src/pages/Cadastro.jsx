import { useState } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import BotaoGoogle from "@/components/auth/BotaoGoogle.jsx";
import CampoSenha from "@/components/auth/CampoSenha.jsx";
import MolduraAuth from "@/components/auth/MolduraAuth.jsx";
import { useAuth } from "@/hooks/useAuth.js";
import { SENHA } from "@/lib/constants.js";
import { googleHabilitado, mensagemErroGoogle } from "@/lib/google.js";
import { getApiError } from "@/utils/apiError.js";
import {
    CPF_MAXLENGTH,
    EMAIL_MAXLENGTH,
    TELEFONE_MAXLENGTH,
    formatCPF,
    formatTelefone,
    isValidCPF,
    isValidEmail,
    isValidTelefone,
    normalizeEmail,
    onlyDigits,
} from "@/utils/masks.js";

/**
 * Criacao de conta pela loja. O cadastro ja devolve sessao — quem se cadastra
 * entra na mesma hora, sem uma segunda tela de login.
 *
 * Papel nao existe neste formulario, nem escondido: quem se cadastra aqui nasce
 * sempre cliente, e a decisao e do servidor.
 *
 * Telefone e CPF sao opcionais, mas quando preenchidos vao SO COM DIGITOS: e
 * assim que ficam gravados, e o backend rejeita mascara.
 */
const VAZIO = {
    nomeCompleto: "",
    email: "",
    senha: "",
    confirmacao: "",
    telefone: "",
    cpf: "",
    aceitaMarketing: false,
};

export default function Cadastro() {
    const [form, setForm] = useState(VAZIO);
    const [erros, setErros] = useState({});
    const [erroGeral, setErroGeral] = useState("");
    const [enviando, setEnviando] = useState(false);

    const { registrar, loginGoogle, estaAutenticado, inicializando } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const destino = location.state?.de?.pathname ?? "/";

    if (inicializando) {
        return (
            <div className="shell flex justify-center py-16 lg:py-24">
                <div className="w-full max-w-sm">
                    <Skeleton className="h-3 w-28" />
                    <Skeleton className="mt-6 h-8 w-48" />
                    <Skeleton className="mt-10 h-12 w-full" />
                    <Skeleton className="mt-6 h-12 w-full" />
                    <Skeleton className="mt-6 h-12 w-full" />
                </div>
            </div>
        );
    }

    if (estaAutenticado) return <Navigate to={destino} replace />;

    function alterar(e) {
        const { name, value, type, checked } = e.target;

        const tratado =
            type === "checkbox"
                ? checked
                : name === "telefone"
                  ? formatTelefone(value)
                  : name === "cpf"
                    ? formatCPF(value)
                    : value;

        setForm((f) => ({ ...f, [name]: tratado }));
        setErros((atual) => ({ ...atual, [name]: undefined }));
    }

    function validar() {
        const novos = {};

        if (form.nomeCompleto.trim().length < 2) {
            novos.nomeCompleto = "Informe seu nome completo.";
        }
        if (!isValidEmail(form.email)) {
            novos.email = "Informe um e-mail válido.";
        }
        if (form.senha.length < SENHA.MIN) {
            novos.senha = `A senha precisa de ao menos ${SENHA.MIN} caracteres.`;
        }
        if (form.confirmacao !== form.senha) {
            novos.confirmacao = "As senhas não são iguais.";
        }
        if (form.telefone && !isValidTelefone(form.telefone)) {
            novos.telefone = "Informe DDD e número.";
        }
        if (form.cpf && !isValidCPF(form.cpf)) {
            novos.cpf = "CPF inválido.";
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
            const usuario = await registrar({
                nomeCompleto: form.nomeCompleto.trim(),
                email: normalizeEmail(form.email),
                senha: form.senha,
                telefone: onlyDigits(form.telefone),
                cpf: onlyDigits(form.cpf),
                aceitaMarketing: form.aceitaMarketing,
            });
            navigate(usuario?.isAdmin ? "/admin" : destino, { replace: true });
        } catch (err) {
            const { message, errors } = getApiError(err);

            // ModelState do .NET vem por campo — devolve cada mensagem ao seu campo.
            if (errors) {
                const porCampo = {};
                for (const [chave, lista] of Object.entries(errors)) {
                    const campo = chave.charAt(0).toLowerCase() + chave.slice(1);
                    porCampo[campo] = Array.isArray(lista) ? lista[0] : String(lista);
                }
                setErros((atual) => ({ ...atual, ...porCampo }));
            }

            setErroGeral(message || "Não foi possível criar sua conta. Tente novamente.");
        } finally {
            setEnviando(false);
        }
    }

    /**
     * Entrar com Google e criar conta com Google sao a MESMA chamada: o backend
     * decide entre vincular a um cadastro existente e abrir um novo. Por isso
     * aqui nao ha validacao de formulario — so a traducao da falha.
     */
    async function entrarComGoogle(credencial) {
        setErroGeral("");
        try {
            const usuario = await loginGoogle(credencial);
            navigate(usuario?.isAdmin ? "/admin" : destino, { replace: true });
        } catch (err) {
            setErroGeral(mensagemErroGoogle(err));
        }
    }

    return (
        <MolduraAuth
            rotulo="Área do cliente"
            titulo="Criar cadastro"
            descricao="Leva menos de um minuto. Depois é só acompanhar seus pedidos por aqui."
            erro={erroGeral}
            rodape={
                <p className="text-ink-soft">
                    Já tem conta?{" "}
                    <Link
                        to="/login"
                        state={location.state}
                        className="text-ink underline decoration-sand underline-offset-4 hover:decoration-ink"
                    >
                        Entrar
                    </Link>
                </p>
            }
        >
            <form onSubmit={submeter} noValidate className="mt-10 flex flex-col gap-6">
                <Campo
                    label="Nome completo"
                    name="nomeCompleto"
                    autoComplete="name"
                    maxLength={180}
                    value={form.nomeCompleto}
                    onChange={alterar}
                    erro={erros.nomeCompleto}
                    obrigatorio
                />

                <Campo
                    label="E-mail"
                    name="email"
                    type="email"
                    autoComplete="email"
                    maxLength={EMAIL_MAXLENGTH}
                    value={form.email}
                    onChange={alterar}
                    onBlur={() => setForm((f) => ({ ...f, email: normalizeEmail(f.email) }))}
                    erro={erros.email}
                    obrigatorio
                />

                <CampoSenha
                    label="Senha"
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
                    label="Repetir a senha"
                    name="confirmacao"
                    autoComplete="new-password"
                    maxLength={SENHA.MAX}
                    value={form.confirmacao}
                    onChange={alterar}
                    erro={erros.confirmacao}
                    obrigatorio
                />

                <Campo
                    label="Telefone"
                    name="telefone"
                    type="tel"
                    inputMode="numeric"
                    autoComplete="tel"
                    placeholder="(11) 90000-0000"
                    maxLength={TELEFONE_MAXLENGTH}
                    value={form.telefone}
                    onChange={alterar}
                    erro={erros.telefone}
                    ajuda="Opcional. Usamos para avisar sobre a entrega."
                />

                <Campo
                    label="CPF"
                    name="cpf"
                    inputMode="numeric"
                    placeholder="000.000.000-00"
                    maxLength={CPF_MAXLENGTH}
                    value={form.cpf}
                    onChange={alterar}
                    erro={erros.cpf}
                    ajuda="Opcional. Necessário apenas na emissão da nota."
                />

                <label className="flex cursor-pointer items-start gap-3 text-sm leading-relaxed text-ink-soft">
                    <input
                        type="checkbox"
                        name="aceitaMarketing"
                        checked={form.aceitaMarketing}
                        onChange={alterar}
                        className="mt-0.5 h-4 w-4 shrink-0 rounded-none border border-sand accent-olive focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-2"
                    />
                    Quero receber novidades e lançamentos por e-mail.
                </label>

                <Botao type="submit" blocoCompleto carregando={enviando}>
                    Criar cadastro
                </Botao>
            </form>

            {googleHabilitado() && (
                <div className="mt-8 flex flex-col gap-6">
                    <div className="flex items-center gap-4" aria-hidden="true">
                        <span className="h-px flex-1 bg-sand" />
                        <span className="eyebrow">ou</span>
                        <span className="h-px flex-1 bg-sand" />
                    </div>

                    <BotaoGoogle
                        onCredencial={entrarComGoogle}
                        onErro={setErroGeral}
                        rotulo="signup_with"
                        desabilitado={enviando}
                    />
                </div>
            )}
        </MolduraAuth>
    );
}
