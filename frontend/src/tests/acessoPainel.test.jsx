import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import Login from "@/pages/Login.jsx";
import RotaAdmin from "@/routes/RotaAdmin.jsx";

/**
 * O caminho de quem tenta abrir o painel sem sessão.
 *
 * A queixa que originou este teste é de uso real: quem digitava /admin sem estar
 * logado caía na tela de entrar padrão, sem uma linha dizendo por quê — a mesma
 * tela de comprar uma blusa. Parecia que o painel tinha sumido.
 *
 * A cadeia toda é exercitada de verdade: RotaAdmin recusa, guarda a rota de
 * origem em `state.de` e o Login lê esse estado para trocar a copy do topo.
 * Testar só a página com o estado montado à mão deixaria passar exatamente o
 * elo que já quebrou antes — o guard esquecer de guardar a origem.
 */
const { auth } = vi.hoisted(() => ({ auth: vi.fn() }));

vi.mock("@/hooks/useAuth.js", () => ({
    useAuth: () => auth(),
    default: () => auth(),
}));

const AVISO_PAINEL = "Entre com uma conta administrativa para acessar o painel.";

/** Sessão no formato que Login e RotaAdmin leem do AuthContext. */
function estado({ papeis = [], login = vi.fn() } = {}) {
    const autenticado = papeis.length > 0;

    return {
        login,
        loginGoogle: vi.fn(),
        estaAutenticado: autenticado,
        inicializando: false,
        usuario: autenticado
            ? { uuid: "uuid-1", papeis, isAdmin: papeis.includes("admin") }
            : null,
    };
}

function montar(inicial, { login = vi.fn(), papeis = [] } = {}) {
    auth.mockReturnValue(estado({ papeis, login }));

    render(
        <MemoryRouter initialEntries={[inicial]}>
            <Routes>
                <Route path="/" element={<p>Vitrine</p>} />
                <Route path="/login" element={<Login />} />
                <Route element={<RotaAdmin />}>
                    <Route path="/admin" element={<p>Painel</p>} />
                </Route>
            </Routes>
        </MemoryRouter>,
    );

    return { user: userEvent.setup(), login };
}

describe("entrada no painel sem sessão", () => {
    it("explica o motivo quando a pessoa veio de /admin", () => {
        montar("/admin");

        expect(screen.getByText(AVISO_PAINEL)).toBeInTheDocument();
        expect(screen.getByText("Painel administrativo")).toBeInTheDocument();
        expect(screen.getByRole("button", { name: "Entrar" })).toBeInTheDocument();
    });

    it("mantém a tela padrão de quem foi direto para /login", () => {
        montar("/login");

        expect(screen.queryByText(AVISO_PAINEL)).not.toBeInTheDocument();
        expect(screen.getByText("Área do cliente")).toBeInTheDocument();
        expect(
            screen.getByText(/Acompanhe pedidos, salve endereços/),
        ).toBeInTheDocument();
    });

    it("não usa a copy do painel para outras rotas privadas", () => {
        montar("/login");

        expect(screen.queryByText(/administrativa/)).not.toBeInTheDocument();
    });

    it("devolve a pessoa ao painel depois de entrar", async () => {
        // Entrar de verdade muda a sessão: sem trocar o estado aqui, a RotaAdmin
        // recusaria de novo e o teste provaria o contrário do que interessa.
        const login = vi.fn(async () => {
            auth.mockReturnValue(estado({ papeis: ["admin"] }));
            return { isAdmin: true };
        });
        const { user } = montar("/admin", { login });

        await user.type(screen.getByLabelText(/E-mail/), "alexandre@glorific.art");
        await user.type(screen.getByLabelText(/Senha/), "senha-secreta");
        await user.click(screen.getByRole("button", { name: "Entrar" }));

        expect(login).toHaveBeenCalledWith({
            email: "alexandre@glorific.art",
            senha: "senha-secreta",
        });
        expect(await screen.findByText("Painel")).toBeInTheDocument();
    });

    it("continua barrando quem já tem sessão, mas não tem papel", () => {
        montar("/admin", { papeis: ["cliente"] });

        // Sem papel administrativo o destino é a vitrine, e não a tela de
        // entrar: mandar para o login quem já está logado sugere "sua sessão
        // expirou", quando o caso é "esta área não é sua".
        expect(screen.getByText("Vitrine")).toBeInTheDocument();
        expect(screen.queryByText(AVISO_PAINEL)).not.toBeInTheDocument();
    });
});
