import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import RotaAdmin from "@/routes/RotaAdmin.jsx";

/**
 * Guarda do painel administrativo.
 *
 * A regra que este teste trava: papel administrativo são TRÊS papéis (admin,
 * gerente e operador), e não apenas "admin". Também trava os dois destinos de
 * recusa, que precisam continuar diferentes — quem não tem sessão vai para a
 * entrada, quem tem sessão sem papel vai para a vitrine.
 */
const { auth } = vi.hoisted(() => ({ auth: vi.fn() }));

vi.mock("@/hooks/useAuth.js", () => ({
    useAuth: () => auth(),
    default: () => auth(),
}));

/** Sessão falsa no mesmo formato que o AuthProvider entrega. */
function sessao({ papeis = [], inicializando = false, autenticado = papeis.length > 0 } = {}) {
    return {
        inicializando,
        estaAutenticado: autenticado,
        usuario: autenticado ? { uuid: "uuid-1", papeis, isAdmin: false } : null,
    };
}

function montar(estado) {
    auth.mockReturnValue(estado);

    return render(
        <MemoryRouter initialEntries={["/admin"]}>
            <Routes>
                <Route path="/" element={<p>Vitrine</p>} />
                <Route path="/login" element={<p>Entrar</p>} />
                <Route element={<RotaAdmin />}>
                    <Route path="/admin" element={<p>Painel</p>} />
                </Route>
            </Routes>
        </MemoryRouter>,
    );
}

describe("guarda do painel", () => {
    it("espera enquanto a sessão está sendo restaurada", () => {
        montar(sessao({ inicializando: true, autenticado: false }));

        expect(screen.getByText("Verificando sua sessão…")).toBeInTheDocument();
        expect(screen.queryByText("Painel")).not.toBeInTheDocument();
        expect(screen.queryByText("Entrar")).not.toBeInTheDocument();
    });

    it("manda para a entrada quem não tem sessão", () => {
        montar(sessao({ autenticado: false }));

        expect(screen.getByText("Entrar")).toBeInTheDocument();
    });

    it("manda para a vitrine quem tem sessão mas é só cliente", () => {
        montar(sessao({ papeis: ["cliente"] }));

        expect(screen.getByText("Vitrine")).toBeInTheDocument();
        expect(screen.queryByText("Entrar")).not.toBeInTheDocument();
    });

    it.each([["admin"], ["gerente"], ["operador"]])("deixa %s entrar no painel", (papel) => {
        montar(sessao({ papeis: [papel] }));

        expect(screen.getByText("Painel")).toBeInTheDocument();
    });

    it("aceita papel administrativo em qualquer posição da lista", () => {
        montar(sessao({ papeis: ["cliente", "gerente"] }));

        expect(screen.getByText("Painel")).toBeInTheDocument();
    });

    it("recusa papel desconhecido, mesmo com sessão válida", () => {
        montar(sessao({ papeis: ["parceiro"] }));

        expect(screen.getByText("Vitrine")).toBeInTheDocument();
    });
});
