import Catalogo from "@/pages/Catalogo.jsx";
import Colecao from "@/pages/Colecao.jsx";
import Colecoes from "@/pages/Colecoes.jsx";
import Produto from "@/pages/Produto.jsx";

/**
 * Rotas da VITRINE (catalogo, produto e colecoes).
 *
 * Todas publicas: a loja precisa abrir para quem nunca entrou, inclusive para o
 * robo de indexacao — e o backend ja declara [AllowAnonymous] nesses endpoints.
 *
 * Entram todas dentro do <LayoutLoja>. A Home continua sendo a rota indice
 * composta pelo integrador; esta area apenas passou a alimenta-la com dados
 * reais (`useDestaques` e `useColecoes`).
 *
 * `/catalogo`, `/busca` e `/categoria/:slug` sao a MESMA page: muda so o
 * recorte e o cabecalho. Manter tres componentes seria manter tres copias da
 * mesma logica de filtro na URL.
 */
const rotasVitrine = [
    { path: "/catalogo", element: <Catalogo />, publica: true },
    { path: "/busca", element: <Catalogo modo="busca" />, publica: true },
    { path: "/categoria/:slug", element: <Catalogo modo="categoria" />, publica: true },
    { path: "/colecoes", element: <Colecoes />, publica: true },
    { path: "/colecao/:slug", element: <Colecao />, publica: true },
    { path: "/produto/:slug", element: <Produto />, publica: true },
];

export default rotasVitrine;
