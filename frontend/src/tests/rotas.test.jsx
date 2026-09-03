import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import AppRoutes from "@/routes/index.jsx";
import { AuthProvider } from "@/contexts/AuthContext.jsx";
import { ToastProvider } from "@/contexts/ToastContext.jsx";
import { CarrinhoProvider } from "@/contexts/CarrinhoContext.jsx";
import { STORE } from "@/data/store.js";

import rotasAuth from "@/routes/rotasAuth.jsx";
import rotasVitrine from "@/routes/rotasVitrine.jsx";
import rotasInstitucional from "@/routes/rotasInstitucional.jsx";
import rotasCompra from "@/routes/rotasCompra.jsx";
import rotasAdminCatalogo from "@/routes/rotasAdminCatalogo.jsx";
import rotasAdminOperacao from "@/routes/rotasAdminOperacao.jsx";

/**
 * Nenhum endereco linkado na interface pode cair em 404.
 *
 * O mapa de rotas e composto a partir de cinco arquivos escritos por frentes
 * diferentes (`rotas<Area>.jsx`). Sem este teste, tirar uma rota de um desses
 * arrays — ou trocar um caminho no `STORE` — quebra um link do cabecalho ou do
 * rodape sem quebrar build, lint nem nenhum outro teste. Foi assim que
 * `/admin/variacoes` chegou a existir no menu lateral sem existir no roteador.
 *
 * O teste NAO exige que a tela carregue dados: sem backend, cada pagina mostra
 * o proprio estado de erro, e rota privada desvia para o login. Nada disso e
 * 404 — e e exatamente essa a fronteira que interessa aqui.
 */

/**
 * Troca o `:param` por um valor qualquer: o que se testa e o casamento da rota.
 *
 * `/politicas/:slug` e a excecao. Ali o parametro nao e um id que a tela vai
 * buscar no servidor: e a escolha de QUAL politica mostrar, e a propria page
 * responde 404 para um slug fora da lista. Um valor inventado seria, com razao,
 * pagina nao encontrada — entao aqui entra um slug que existe de verdade.
 */
const VALOR_DE_EXEMPLO = {
    "/politicas/:slug": "/politicas/trocas",
};

function concretizar(caminho) {
    return VALOR_DE_EXEMPLO[caminho] ?? caminho.replace(/:[^/]+/g, "exemplo");
}

function renderizarEm(caminho) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    // Sem backend, React Query e o axios reclamam no console. O ruido nao e o
    // objeto do teste.
    const silencio = vi.spyOn(console, "error").mockImplementation(() => {});

    render(
        <QueryClientProvider client={queryClient}>
            <AuthProvider>
                <MemoryRouter initialEntries={[caminho]}>
                    <ToastProvider>
                        <CarrinhoProvider>
                            <AppRoutes />
                        </CarrinhoProvider>
                    </ToastProvider>
                </MemoryRouter>
            </AuthProvider>
        </QueryClientProvider>,
    );

    silencio.mockRestore();
}

/** Caminhos que o Header e o Footer linkam de verdade. */
const linksDaInterface = [
    "/",
    ...STORE.navegacao.map((i) => i.to),
    ...STORE.institucional.map((i) => i.to),
    "/carrinho",
    "/conta",
    "/admin",
    "/busca?q=linho",
];

const rotasDeclaradas = [
    ...rotasAuth,
    ...rotasVitrine,
    ...rotasInstitucional,
    ...rotasCompra,
    ...rotasAdminCatalogo,
    ...rotasAdminOperacao,
].map((r) => r.path);

const todos = [...new Set([...linksDaInterface, ...rotasDeclaradas].map(concretizar))];

describe("nenhuma rota da interface cai em 404", () => {
    it.each(todos)("%s", (caminho) => {
        renderizarEm(caminho);

        expect(screen.queryByText(/Página não encontrada/i)).toBeNull();
    });

    it("um endereço inventado, esse sim, cai em 404", () => {
        renderizarEm("/isto-nao-existe");

        expect(screen.getByText(/Página não encontrada/i)).toBeTruthy();
    });

    it("uma política que não existe cai em 404, e não numa tela vazia", () => {
        // A rota `/politicas/:slug` casa com qualquer coisa. Quem decide se o
        // slug existe e a page — e o erro caro aqui seria ela renderizar o
        // chassi da política com o miolo em branco.
        renderizarEm("/politicas/slug-que-nao-existe");

        expect(screen.getByText(/Página não encontrada/i)).toBeTruthy();
    });

    it.each(["trocas", "entrega", "privacidade", "termos"])(
        "a política /politicas/%s tem texto de verdade",
        (slug) => {
            renderizarEm(`/politicas/${slug}`);

            expect(screen.queryByText(/Página não encontrada/i)).toBeNull();
            // Institucional nao depende de backend: se ha rota e nao ha 404, o
            // texto tem de estar na tela agora, sem carregamento nenhum.
            expect(document.body.textContent.length).toBeGreaterThan(500);
        },
    );

    it("o painel exige sessão antes de qualquer coisa", () => {
        // Sem sessao, /admin nao chega a resolver a tela: a guarda desvia para o
        // login. Vale para o caminho inventado e para o valido — e por isso o
        // catch-all do grupo /admin nao da para exercitar sem um login de
        // verdade. Aqui se afirma so o que este teste consegue provar.
        renderizarEm("/admin/isto-nao-existe");

        // A guarda espera a sessao ser restaurada antes de decidir (o access
        // token vive em memoria e so existe depois do refresh silencioso).
        // Enquanto isso, quem esta na tela e o esqueleto de espera.
        expect(screen.getByText(/Verificando sua sessão/i)).toBeTruthy();
    });
});

describe("os arrays de rota não se atropelam", () => {
    it("nenhum caminho repetido na loja", () => {
        const loja = [...rotasAuth, ...rotasVitrine, ...rotasCompra].map((r) => r.path);
        expect(loja.filter((p, i) => loja.indexOf(p) !== i)).toEqual([]);
    });

    it("nenhum caminho repetido no painel", () => {
        const admin = [...rotasAdminCatalogo, ...rotasAdminOperacao].map((r) => r.path);
        expect(admin.filter((p, i) => admin.indexOf(p) !== i)).toEqual([]);
    });

    it("toda rota do painel comeca em /admin", () => {
        for (const rota of [...rotasAdminCatalogo, ...rotasAdminOperacao]) {
            expect(rota.path.startsWith("/admin")).toBe(true);
        }
    });
});
