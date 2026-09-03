import Sobre from "@/pages/Sobre.jsx";
import GuiaMedidas from "@/pages/GuiaMedidas.jsx";
import Politica from "@/pages/Politica.jsx";

/**
 * Rotas INSTITUCIONAIS, para o integrador compor em `routes/index.jsx`
 * (mesmo formato dos demais arrays: `{ path, element, publica }`).
 *
 * Substituem os tres destinos que ficavam em `EmBreve`. Todas dentro do
 * `<LayoutLoja>` e todas PUBLICAS, por dois motivos: sao os enderecos que o
 * Header e o Footer linkam para quem ainda nao tem conta, e sao justamente as
 * paginas que o robo de busca precisa ler (politica de troca e guia de medidas
 * pesam em busca de moda). O endpoint por tras do guia e `[AllowAnonymous]`.
 *
 * `/politicas/:slug` e UMA page com varias politicas em vez de quatro pages:
 * muda so o texto, e o cabecalho, o sumario lateral e o bloco de contato sao os
 * mesmos. Slug desconhecido cai na tela de 404 da loja, decidido dentro da
 * propria page — nao ha rota curinga a mais aqui.
 */
const rotasInstitucional = [
    { path: "/sobre", element: <Sobre />, publica: true },
    { path: "/guia-de-medidas", element: <GuiaMedidas />, publica: true },
    { path: "/politicas/:slug", element: <Politica />, publica: true },
];

export default rotasInstitucional;
