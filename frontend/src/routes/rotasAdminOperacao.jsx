import { lazy } from "react";
import { POLITICAS } from "@/lib/permissoes.js";

/* Carregamento tardio — ver a nota em `rotasAdminCatalogo.jsx`. */
const Dashboard = lazy(() => import("@/pages/admin/Dashboard.jsx"));
const ListaPedidos = lazy(() => import("@/pages/admin/pedidos/ListaPedidos.jsx"));
const DetalhePedido = lazy(() => import("@/pages/admin/pedidos/DetalhePedido.jsx"));
const Estoque = lazy(() => import("@/pages/admin/Estoque.jsx"));
const Cupons = lazy(() => import("@/pages/admin/Cupons.jsx"));
const Avaliacoes = lazy(() => import("@/pages/admin/Avaliacoes.jsx"));
const Configuracoes = lazy(() => import("@/pages/admin/Configuracoes.jsx"));
const Usuarios = lazy(() => import("@/pages/admin/Usuarios.jsx"));

/**
 * Rotas da área "painel admin — operação".
 *
 * Contrato combinado com o integrador: array de
 * `{ path, element, publica?, policy? }`. Nenhuma é pública — todas vivem
 * dentro de `LayoutAdmin`.
 *
 * Três observações para quem compõe o `routes/index.jsx`:
 *
 * 1. Os caminhos são ABSOLUTOS e já começam por "/admin". Isso funciona tanto
 *    soltos quanto aninhados sob um `<Route path="/admin">`, porque o React
 *    Router aceita caminho absoluto de filho desde que ele comece pelo caminho
 *    do pai.
 *
 * 2. `policy` traz o NOME EXATO da policy do backend. A guarda pronta é
 *    `routes/RotaPolicy.jsx`:
 *
 *        <Route element={<RotaPolicy policy={rota.policy} />}>
 *            <Route path={rota.path} element={rota.element} />
 *        </Route>
 *
 * 3. A guarda `RotaAdmin` atual exige `role === "admin"` e barra gerente e
 *    operador na porta do painel, apesar de o servidor liberar os dois. Trocá-la
 *    por `RotaPolicy policy="PainelAdmin"` no grupo /admin é o que faz a
 *    filtragem de menu por papel ter algum efeito prático.
 */
const rotasAdminOperacao = [
    {
        // Mesma tela que o `index` de /admin já aponta hoje. Se o grupo /admin
        // continuar com `<Route index>`, esta entrada é a duplicata a descartar.
        path: "/admin",
        element: <Dashboard />,
        policy: POLITICAS.PAINEL_ADMIN,
    },
    {
        path: "/admin/pedidos",
        element: <ListaPedidos />,
        policy: POLITICAS.EXPEDICAO,
    },
    {
        path: "/admin/pedidos/:uuid",
        element: <DetalhePedido />,
        policy: POLITICAS.EXPEDICAO,
    },
    {
        path: "/admin/estoque",
        element: <Estoque />,
        policy: POLITICAS.EXPEDICAO,
    },
    {
        path: "/admin/cupons",
        element: <Cupons />,
        policy: POLITICAS.GESTAO_CATALOGO,
    },
    {
        path: "/admin/avaliacoes",
        element: <Avaliacoes />,
        policy: POLITICAS.GESTAO_CATALOGO,
    },
    {
        path: "/admin/configuracoes",
        element: <Configuracoes />,
        policy: POLITICAS.SOMENTE_ADMIN,
    },
    {
        path: "/admin/usuarios",
        element: <Usuarios />,
        policy: POLITICAS.SOMENTE_ADMIN,
    },
];

export default rotasAdminOperacao;
