import { useState } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import BotaoGoogle from "@/components/auth/BotaoGoogle.jsx";
import CampoSenha from "@/components/auth/CampoSenha.jsx";
import MolduraAuth from "@/components/auth/MolduraAuth.jsx";
import { useAuth } from "@/hooks/useAuth.js";
import { googleHabilitado, mensagemErroGoogle } from "@/lib/google.js";
import { getApiError } from "@/utils/apiError.js";
import { EMAIL_MAXLENGTH, isValidEmail, normalizeEmail } from "@/utils/masks.js";

/**
 * Entrada por e-mail e senha, com Google como atalho.
 *
 * A page nao conhece o service nem o axios: fala com `useAuth()`.
 *
 * O erro de credencial aparece AQUI, e nao em toast: 401 e o unico status que o
 * interceptor nao anuncia, justamente para a tela que causou o erro ser quem o
 * explica, ao lado dos campos. O mesmo vale para o Google, que pede silencio ao
 * interceptor (`__semToast`) e mostra a falha traduzida neste formulario.
 *
 * A tela tambem muda de texto conforme QUEM mandou a pessoa para ca. Quem tentou
 * abrir /admin sem sessao precisa ler o motivo — sem isso o painel simplesmente
 * "vira a tela de login", e o proprio dono da loja fica sem entender se errou o
 * endereco, se perdeu o acesso ou se o sistema quebrou.
 */

/** Copy do topo, por origem. `de` e o caminho guardado pelo guard de rota. */
function contexto(de) {
    if (de.startsWith("/admin")) {
        return {
            rotulo: "Painel administrativo",
            titulo: "Entrar",
            descricao: "Entre com uma conta administrativa para acessar o painel.",
        };
    }

    if (de.startsWith("/checkout")) {
        return {
            rotulo: "Finalizar compra",
            titulo: "Entrar",
            descricao: "Entre para concluir seu pedido com endereço e frete salvos.",
        };
    }

    return {
        rotulo: "Área do cliente",
        titulo: "Entrar",
        descricao: "Acompanhe pedidos, salve endereços e guarde suas peças favoritas.",
    };
}

export default function Login() {
    const [form, setForm] = useState({ email: "", senha: "" });
    const [erros, setErros] = useState({});
    const [erroGeral, setErroGeral] = useState("");
    const [enviando, setEnviando] = useState(false);

    const { login, loginGoogle, estaAutenticado, inicializando } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    // Para onde voltar depois de entrar (guardado pela RotaPrivada/RotaAdmin).
    const destino = location.state?.de?.pathname ?? "/";
    const copy = contexto(destino);

    // Enquanto a sessao e restaurada nao da para saber se esta tela e necessaria.
    if (inicializando) {
        return (
            <div className="shell flex justify-center py-16 lg:py-24">
                <div className="w-full max-w-sm">
                    <Skeleton className="h-3 w-28" />
                    <Skeleton className="mt-6 h-8 w-40" />
                    <Skeleton className="mt-10 h-12 w-full" />
                    <Skeleton className="mt-6 h-12 w-full" />
                    <Skeleton className="mt-8 h-11 w-full" />
                </div>
            </div>
        );
    }

    // Quem ja tem sessao nao precisa ver formulario de entrada.
    if (estaAutenticado) return <Navigate to={destino} replace />;

    function alterar(e) {
        const { name, value } = e.target;
        setForm((f) => ({ ...f, [name]: value }));
        setErros((atual) => ({ ...atual, [name]: undefined }));
    }

    function validar() {
        const novos = {};
        if (!isValidEmail(form.email)) novos.email = "Informe um e-mail válido.";
        if (!form.senha) novos.senha = "Informe sua senha.";
        setErros(novos);
        return Object.keys(novos).length === 0;
    }

    /** Traducao unica de falha em texto de tela, para os dois caminhos de entrada. */
    function tratarFalha(err) {
        const { status, message } = getApiError(err);
        setErroGeral(
            status === 401
                ? "E-mail ou senha incorretos."
                : message || "Não foi possível entrar. Tente novamente.",
        );
    }

    function concluir(usuario) {
        navigate(usuario?.isAdmin ? "/admin" : destino, { replace: true });
    }

    async function submeter(e) {
        e.preventDefault();
        setErroGeral("");
        if (!validar()) return;

        setEnviando(true);
        try {
            concluir(await login({ email: normalizeEmail(form.email), senha: form.senha }));
        } catch (err) {
            tratarFalha(err);
        } finally {
            setEnviando(false);
        }
    }

    async function entrarComGoogle(credencial) {
        setErroGeral("");
        try {
            concluir(await loginGoogle(credencial));
        } catch (err) {
            // Falha do Google tem vocabulario proprio: "conta desativada",
            // "dominio nao autorizado", "Google fora do ar". Passar por
            // `tratarFalha` viraria "e-mail ou senha incorretos", que aqui e
            // simplesmente mentira.
            setErroGeral(mensagemErroGoogle(err));
        }
    }

    return (
        <MolduraAuth
            rotulo={copy.rotulo}
            titulo={copy.titulo}
            descricao={copy.descricao}
            erro={erroGeral}
            rodape={
                <>
                    <Link
                        to="/esqueci-senha"
                        className="text-ink-soft underline decoration-sand underline-offset-4 transition-colors hover:text-ink"
                    >
                        Esqueci minha senha
                    </Link>
                    <p className="text-ink-soft">
                        Ainda não tem conta?{" "}
                        <Link
                            to="/cadastro"
                            state={location.state}
                            className="text-ink underline decoration-sand underline-offset-4 hover:decoration-ink"
                        >
                            Criar cadastro
                        </Link>
                    </p>
                </>
            }
        >
            <form onSubmit={submeter} noValidate className="mt-10 flex flex-col gap-6">
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
                    autoComplete="current-password"
                    value={form.senha}
                    onChange={alterar}
                    erro={erros.senha}
                    obrigatorio
                />

                <Botao type="submit" blocoCompleto carregando={enviando}>
                    Entrar
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
                        desabilitado={enviando}
                    />
                </div>
            )}
        </MolduraAuth>
    );
}
