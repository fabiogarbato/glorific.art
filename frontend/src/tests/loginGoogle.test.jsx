import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import Login from "@/pages/Login.jsx";
import Cadastro from "@/pages/Cadastro.jsx";
import ProvedorGoogle from "@/components/auth/ProvedorGoogle.jsx";
import { mensagemErroGoogle } from "@/lib/google.js";

/**
 * Login com Google, ponta a ponta do lado do front.
 *
 * Três coisas ficam travadas aqui:
 *
 * 1. O bloco do Google some por inteiro sem `VITE_GOOGLE_CLIENT_ID` e aparece
 *    com ele. Renderizar o widget sem client id derruba a tela de sessão com
 *    erro do script do Google — e aí não se entra nem por e-mail e senha.
 * 2. O `credential` (id_token) chega ao `authService` e a pessoa volta para a
 *    rota de origem, igual ao login por senha.
 * 3. Falha do backend vira frase legível NA TELA. Toast genérico no canto não
 *    serve: "esta conta está desativada" precisa ser lido ao lado do botão que
 *    acabou de ser clicado.
 *
 * O SDK do Google é dublado — o que se testa é o nosso contrato com ele
 * (recebeu credential -> chamou o serviço), não o widget deles.
 */
const CLIENT_ID = "123456789-teste.apps.googleusercontent.com";

vi.mock("@react-oauth/google", () => ({
    GoogleOAuthProvider: ({ clientId, children }) => (
        <div data-testid="provedor-google" data-client-id={clientId}>
            {children}
        </div>
    ),
    GoogleLogin: ({ onSuccess, onError }) => (
        <div>
            <button type="button" onClick={() => onSuccess({ credential: "id-token-do-google" })}>
                Entrar com Google
            </button>
            <button type="button" onClick={() => onSuccess({})}>
                Google sem credencial
            </button>
            <button type="button" onClick={() => onError()}>
                Google falhou
            </button>
        </div>
    ),
}));

const { auth } = vi.hoisted(() => ({ auth: vi.fn() }));

vi.mock("@/hooks/useAuth.js", () => ({
    useAuth: () => auth(),
    default: () => auth(),
}));

/** Erro do axios como o interceptor entrega: `{ response: { status, data } }`. */
function erroApi(status, mensagem) {
    return { response: { status, data: { statusCode: status, error: mensagem } } };
}

function montar(Tela, { loginGoogle = vi.fn(), origem } = {}) {
    auth.mockReturnValue({
        login: vi.fn(),
        registrar: vi.fn(),
        loginGoogle,
        estaAutenticado: false,
        inicializando: false,
    });

    const entrada = origem
        ? [{ pathname: "/login", state: { de: { pathname: origem } } }]
        : ["/login"];

    render(
        <MemoryRouter initialEntries={entrada}>
            <Routes>
                <Route path="/login" element={<Tela />} />
                <Route path="/" element={<p>Vitrine</p>} />
                <Route path="/conta" element={<p>Minha conta</p>} />
                <Route path="/admin" element={<p>Painel</p>} />
            </Routes>
        </MemoryRouter>,
    );

    return { user: userEvent.setup(), loginGoogle };
}

afterEach(() => {
    vi.unstubAllEnvs();
});

describe("presença do botão do Google", () => {
    it("some da tela de entrar quando não há client id", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "");
        montar(Login);

        expect(screen.queryByText("Entrar com Google")).not.toBeInTheDocument();
        // O separador "ou" também some: sem o atalho ele não separa nada.
        expect(screen.queryByText("ou")).not.toBeInTheDocument();
        // E a entrada por e-mail e senha continua de pé — este é o ponto.
        expect(screen.getByRole("button", { name: "Entrar" })).toBeInTheDocument();
    });

    it("aparece na tela de entrar quando há client id", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        montar(Login);

        expect(screen.getByText("Entrar com Google")).toBeInTheDocument();
        expect(screen.getByText("ou")).toBeInTheDocument();
    });

    it("aparece também na tela de criar cadastro", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        montar(Cadastro);

        expect(screen.getByText("Entrar com Google")).toBeInTheDocument();
    });

    it("some da tela de criar cadastro quando não há client id", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "");
        montar(Cadastro);

        expect(screen.queryByText("Entrar com Google")).not.toBeInTheDocument();
        expect(screen.getByRole("button", { name: "Criar cadastro" })).toBeInTheDocument();
    });

    it("espaço em branco no client id conta como ausente", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "   ");
        montar(Login);

        expect(screen.queryByText("Entrar com Google")).not.toBeInTheDocument();
    });
});

describe("provider do Google", () => {
    it("monta o provider uma vez, com o client id configurado", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);

        render(
            <ProvedorGoogle>
                <p>Loja</p>
            </ProvedorGoogle>,
        );

        const provedores = screen.getAllByTestId("provedor-google");
        expect(provedores).toHaveLength(1);
        expect(provedores[0]).toHaveAttribute("data-client-id", CLIENT_ID);
        expect(screen.getByText("Loja")).toBeInTheDocument();
    });

    it("não monta o provider sem client id, e a árvore segue de pé", () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "");

        render(
            <ProvedorGoogle>
                <p>Loja</p>
            </ProvedorGoogle>,
        );

        expect(screen.queryByTestId("provedor-google")).not.toBeInTheDocument();
        expect(screen.getByText("Loja")).toBeInTheDocument();
    });
});

describe("entrada com Google", () => {
    it("manda o id_token para a sessão e volta para a rota de origem", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const loginGoogle = vi.fn().mockResolvedValue({ isAdmin: false });
        const { user } = montar(Login, { loginGoogle, origem: "/conta" });

        await user.click(screen.getByText("Entrar com Google"));

        expect(loginGoogle).toHaveBeenCalledWith("id-token-do-google");
        expect(await screen.findByText("Minha conta")).toBeInTheDocument();
    });

    it("leva quem tem papel administrativo direto para o painel", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const loginGoogle = vi.fn().mockResolvedValue({ isAdmin: true });
        const { user } = montar(Login, { loginGoogle });

        await user.click(screen.getByText("Entrar com Google"));

        expect(await screen.findByText("Painel")).toBeInTheDocument();
    });

    it("avisa na tela quando o Google não devolve credencial", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const loginGoogle = vi.fn();
        const { user } = montar(Login, { loginGoogle });

        await user.click(screen.getByText("Google sem credencial"));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "O Google não devolveu as credenciais. Tente novamente.",
        );
        expect(loginGoogle).not.toHaveBeenCalled();
    });

    it("avisa na tela quando o widget do Google falha", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const { user } = montar(Login);

        await user.click(screen.getByText("Google falhou"));

        expect(await screen.findByRole("alert")).toHaveTextContent(/Google/);
    });
});

describe("erro do backend no login com Google", () => {
    it("mostra conta desativada com acento, e não a frase crua do servidor", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const loginGoogle = vi
            .fn()
            .mockRejectedValue(erroApi(400, "Esta conta esta desativada. Fale com o atendimento."));
        const { user } = montar(Login, { loginGoogle });

        await user.click(screen.getByText("Entrar com Google"));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "Esta conta está desativada. Fale com o atendimento para reativá-la.",
        );
    });

    it("não confunde 401 do Google com senha errada", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        // O 401 do backend é genérico de propósito; a tela não pode repetir
        // "e-mail ou senha incorretos" para quem clicou no botão do Google.
        const loginGoogle = vi.fn().mockRejectedValue(erroApi(401, "Credencial recusada."));
        const { user } = montar(Login, { loginGoogle });

        await user.click(screen.getByText("Entrar com Google"));

        const aviso = await screen.findByRole("alert");
        expect(aviso).toHaveTextContent("O Google não confirmou esta identidade.");
        expect(aviso).not.toHaveTextContent("senha");
    });

    it("diz para usar senha quando o Google não está configurado no servidor", async () => {
        vi.stubEnv("VITE_GOOGLE_CLIENT_ID", CLIENT_ID);
        const loginGoogle = vi
            .fn()
            .mockRejectedValue(erroApi(500, "Ocorreu um erro inesperado. Informe o traceId ao suporte."));
        const { user } = montar(Login, { loginGoogle });

        await user.click(screen.getByText("Entrar com Google"));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "O login com Google está indisponível no momento. Entre com e-mail e senha.",
        );
    });
});

describe("tradução de erro do Google", () => {
    it("cobre os casos que a tela precisa explicar", () => {
        expect(mensagemErroGoogle(erroApi(401, "qualquer coisa"))).toMatch(
            /não confirmou esta identidade/i,
        );
        expect(mensagemErroGoogle(erroApi(403, "Acesso negado."))).toMatch(/permissão/i);
        expect(mensagemErroGoogle(erroApi(500, "erro"))).toMatch(/indisponível/i);
        // As regras casam com acento e sem acento de propósito: o backend
        // escreve sem, e a redação dele pode mudar sem aviso.
        expect(
            mensagemErroGoogle(erroApi(400, "E-mail da conta Google não verificado.")),
        ).toMatch(/não confirmou o e-mail/i);
        expect(
            mensagemErroGoogle(erroApi(400, "Esta conta Google pertence a domínio bloqueado.")),
        ).toMatch(/domínio autorizado/i);
        // Sem resposta = rede fora. Não adianta mandar tentar o mesmo botão.
        expect(mensagemErroGoogle(new Error("Network Error"))).toMatch(/conexão/i);
    });

    it("preserva mensagem de regra de negócio que ainda não tem tradução", () => {
        expect(mensagemErroGoogle(erroApi(400, "Cadastro bloqueado para esta origem."))).toBe(
            "Cadastro bloqueado para esta origem.",
        );
    });
});
