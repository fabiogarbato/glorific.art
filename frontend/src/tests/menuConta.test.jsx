import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import MenuConta from "@/components/layout/MenuConta.jsx";
import { PAPEIS_ADMINISTRATIVOS, ROLES } from "@/lib/constants.js";

/**
 * Menu de conta do cabeçalho — a única porta de entrada do painel na loja.
 *
 * A regra que este arquivo trava é de acesso, não de enfeite: "Painel
 * administrativo" aparece para admin, gerente e operador, e para mais ninguém.
 * Antes deste menu não existia link nenhum para /admin na loja, e quem
 * administra tinha de digitar a URL na mão; se o item sumir de novo, ou se
 * vazar para cliente, é aqui que a regressão precisa aparecer.
 *
 * Esconder o item NÃO é segurança — a RotaAdmin e a policy do servidor é que
 * barram o acesso. É honestidade de interface: nada de mostrar porta que não
 * abre, nem esconder porta que a pessoa tem direito de usar.
 */
const { auth } = vi.hoisted(() => ({ auth: vi.fn() }));

vi.mock("@/hooks/useAuth.js", () => ({
    useAuth: () => auth(),
    default: () => auth(),
}));

const logout = vi.fn();

/** Sessão no mesmo formato que o AuthContext entrega ao componente. */
function sessao({ papeis = [], nome = "Alexandre Marinho", email = "alexandre@glorific.art" } = {}) {
    const autenticado = papeis.length > 0;
    const administrativo = papeis.some((p) => PAPEIS_ADMINISTRATIVOS.includes(p));

    return {
        usuario: autenticado ? { nome, email, papeis, isAdmin: administrativo } : null,
        isAdmin: administrativo,
        logout,
    };
}

function montar(estado) {
    auth.mockReturnValue(estado);

    return {
        user: userEvent.setup(),
        ...render(
            <MemoryRouter>
                <MenuConta />
                <p>Fora do menu</p>
            </MemoryRouter>,
        ),
    };
}

/** O botão do ícone de pessoa, com o rótulo que ele tem em cada estado. */
function botao() {
    return screen.getByRole("button", { name: /minha conta|entrar ou criar cadastro/i });
}

async function abrir(user) {
    await user.click(botao());
    return screen.getByRole("menu", { name: "Conta" });
}

beforeEach(() => {
    logout.mockReset();
});

describe("menu de conta", () => {
    it("começa fechado e não mostra item nenhum", () => {
        montar(sessao({ papeis: [ROLES.CLIENTE] }));

        expect(botao()).toHaveAttribute("aria-expanded", "false");
        expect(screen.queryByRole("menu")).not.toBeInTheDocument();
        expect(screen.queryByText("Minha conta")).not.toBeInTheDocument();
    });

    it("oferece entrar e criar cadastro para quem não tem sessão", async () => {
        const { user } = montar(sessao());
        await abrir(user);

        expect(screen.getByRole("menuitem", { name: "Entrar" })).toHaveAttribute(
            "href",
            "/login",
        );
        expect(screen.getByRole("menuitem", { name: "Criar cadastro" })).toHaveAttribute(
            "href",
            "/cadastro",
        );
        expect(screen.queryByRole("menuitem", { name: "Sair" })).not.toBeInTheDocument();
        expect(
            screen.queryByRole("menuitem", { name: "Painel administrativo" }),
        ).not.toBeInTheDocument();
    });

    it("mostra a área do cliente e a saída para quem está logado", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        expect(screen.getByRole("menuitem", { name: "Minha conta" })).toHaveAttribute(
            "href",
            "/conta",
        );
        expect(screen.getByRole("menuitem", { name: "Meus pedidos" })).toHaveAttribute(
            "href",
            "/conta/pedidos",
        );
        expect(screen.getByRole("menuitem", { name: "Lista de desejos" })).toHaveAttribute(
            "href",
            "/conta/lista-desejos",
        );
        expect(screen.getByRole("menuitem", { name: "Sair" })).toBeInTheDocument();
        expect(screen.getByText(/Olá, Alexandre/)).toBeInTheDocument();
    });

    it("NÃO mostra o painel para cliente", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        expect(
            screen.queryByRole("menuitem", { name: "Painel administrativo" }),
        ).not.toBeInTheDocument();
    });

    it.each([[ROLES.ADMIN], [ROLES.GERENTE], [ROLES.OPERADOR]])(
        "mostra o painel para %s, apontando para /admin",
        async (papel) => {
            const { user } = montar(sessao({ papeis: [papel] }));
            await abrir(user);

            expect(screen.getByRole("menuitem", { name: "Painel administrativo" })).toHaveAttribute(
                "href",
                "/admin",
            );
            // O papel administrativo não substitui a área do cliente: quem
            // administra também compra.
            expect(screen.getByRole("menuitem", { name: "Meus pedidos" })).toBeInTheDocument();
        },
    );

    it("aceita papel administrativo em qualquer posição da lista", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE, ROLES.OPERADOR] }));
        await abrir(user);

        expect(
            screen.getByRole("menuitem", { name: "Painel administrativo" }),
        ).toBeInTheDocument();
    });

    it("não mostra o painel para papel desconhecido", async () => {
        const { user } = montar(sessao({ papeis: ["parceiro"] }));
        await abrir(user);

        expect(
            screen.queryByRole("menuitem", { name: "Painel administrativo" }),
        ).not.toBeInTheDocument();
    });

    it("chama a saída da sessão ao clicar em Sair", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        await user.click(screen.getByRole("menuitem", { name: "Sair" }));

        expect(logout).toHaveBeenCalledWith({ redirecionar: true });
    });
});

describe("acessibilidade do menu de conta", () => {
    it("anuncia o estado em aria-expanded", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.ADMIN] }));

        expect(botao()).toHaveAttribute("aria-expanded", "false");
        expect(botao()).toHaveAttribute("aria-haspopup", "menu");

        await abrir(user);
        expect(botao()).toHaveAttribute("aria-expanded", "true");

        await user.click(botao());
        expect(botao()).toHaveAttribute("aria-expanded", "false");
    });

    it("põe o foco no primeiro item ao abrir", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        expect(screen.getByRole("menuitem", { name: "Minha conta" })).toHaveFocus();
    });

    it("fecha com Esc e devolve o foco para o botão", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        await user.keyboard("{Escape}");

        expect(screen.queryByRole("menu")).not.toBeInTheDocument();
        expect(botao()).toHaveFocus();
    });

    it("fecha com clique fora", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.CLIENTE] }));
        await abrir(user);

        await user.click(screen.getByText("Fora do menu"));

        expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    });

    it("abre pelo teclado e percorre os itens com as setas", async () => {
        const { user } = montar(sessao({ papeis: [ROLES.ADMIN] }));

        botao().focus();
        await user.keyboard("{ArrowDown}");

        // Com papel administrativo o painel é o primeiro item — é o destaque.
        expect(screen.getByRole("menuitem", { name: "Painel administrativo" })).toHaveFocus();

        await user.keyboard("{ArrowDown}");
        expect(screen.getByRole("menuitem", { name: "Minha conta" })).toHaveFocus();

        await user.keyboard("{ArrowUp}");
        expect(screen.getByRole("menuitem", { name: "Painel administrativo" })).toHaveFocus();

        // A lista dá a volta: para cima, do primeiro vai ao último.
        await user.keyboard("{ArrowUp}");
        expect(screen.getByRole("menuitem", { name: "Sair" })).toHaveFocus();

        await user.keyboard("{Home}");
        expect(screen.getByRole("menuitem", { name: "Painel administrativo" })).toHaveFocus();

        await user.keyboard("{End}");
        expect(screen.getByRole("menuitem", { name: "Sair" })).toHaveFocus();
    });
});
