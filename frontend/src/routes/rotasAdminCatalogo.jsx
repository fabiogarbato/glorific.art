import { lazy } from "react";

/*
 * Carregamento tardio: nenhuma destas telas pertence ao caminho de compra, e
 * quem entra na loja nao deve baixar o painel junto. O <Suspense> que cobre
 * estes elementos fica no `routes/index.jsx`, dentro do LayoutAdmin.
 */
const Categorias = lazy(() => import("@/pages/admin/Categorias.jsx"));
const Colecoes = lazy(() => import("@/pages/admin/Colecoes.jsx"));
const Cores = lazy(() => import("@/pages/admin/Cores.jsx"));
const Midias = lazy(() => import("@/pages/admin/Midias.jsx"));
const TabelasMedidas = lazy(() => import("@/pages/admin/TabelasMedidas.jsx"));
const Tamanhos = lazy(() => import("@/pages/admin/Tamanhos.jsx"));
const FormProduto = lazy(() => import("@/pages/admin/produtos/FormProduto.jsx"));
const ListaProdutos = lazy(() => import("@/pages/admin/produtos/ListaProdutos.jsx"));

/**
 * Rotas do painel administrativo de catálogo.
 *
 * O integrador compõe este array dentro do grupo que já existe:
 * `<RotaAdmin>` (guarda de papel) + `<LayoutAdmin>` (chassi com a barra
 * lateral). Nenhuma rota daqui é pública.
 *
 * Os caminhos são ABSOLUTOS de propósito. Em react-router v7 um filho com
 * caminho absoluto precisa estender o caminho do pai, e todos aqui começam em
 * `/admin` — assim o array funciona tanto aninhado sob a rota `/admin` quanto
 * declarado no nível de cima, sem o integrador ter que adivinhar se o caminho
 * era relativo.
 *
 * `policy` espelha a policy do backend: todos os controllers desta área usam
 * `PoliticasAutorizacao.GestaoCatalogo` (admin ou gerente). O front não decide
 * autorização — quem decide é a API —, mas a informação permite ao integrador
 * esconder o item de menu de quem não pode entrar.
 */
const POLICY = "GestaoCatalogo";

const rotasAdminCatalogo = [
    {
        path: "/admin/produtos",
        element: <ListaProdutos />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/produtos/novo",
        element: <FormProduto />,
        publica: false,
        policy: POLICY,
    },
    {
        // Edição da peça: dados, matriz de variações e galeria, em abas.
        path: "/admin/produtos/:id",
        element: <FormProduto />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/categorias",
        element: <Categorias />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/colecoes",
        element: <Colecoes />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/tamanhos",
        element: <Tamanhos />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/cores",
        element: <Cores />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/tabelas-medidas",
        element: <TabelasMedidas />,
        publica: false,
        policy: POLICY,
    },
    {
        path: "/admin/midias",
        element: <Midias />,
        publica: false,
        policy: POLICY,
    },
];

export default rotasAdminCatalogo;
