import Carrinho from "@/pages/Carrinho.jsx";
import Checkout from "@/pages/Checkout.jsx";
import PagamentoRetorno from "@/pages/PagamentoRetorno.jsx";
import Perfil from "@/pages/conta/Perfil.jsx";
import Enderecos from "@/pages/conta/Enderecos.jsx";
import MeusPedidos from "@/pages/conta/MeusPedidos.jsx";
import DetalhePedido from "@/pages/conta/DetalhePedido.jsx";
import ListaDesejos from "@/pages/conta/ListaDesejos.jsx";

/**
 * Rotas de COMPRA e CONTA, para o integrador compor em `routes/index.jsx`.
 * Todas pertencem ao chassi da loja (`LayoutLoja`).
 *
 * `/carrinho` é PÚBLICA e isso é decisão de negócio, não descuido: o visitante
 * monta a sacola antes de ter conta (o backend identifica o carrinho anônimo por
 * cookie httpOnly), e exigir login para isso é o jeito mais rápido de perder a
 * venda. O login só é cobrado no checkout.
 *
 * `/checkout/retorno` é o caminho FIXO para onde o backend manda o navegador
 * depois da InfinitePay — ver `WebhooksController.RetornoPagamento`, que faz
 * `Redirect(UrlLoja("checkout/retorno?resultado=..."))`. Renomear esta rota
 * quebra a volta do cliente que acabou de pagar.
 *
 * `/conta` é a rota índice do perfil; as demais telas da área ficam abaixo dela.
 */
const rotasCompra = [
    { path: "/carrinho", element: <Carrinho />, publica: true },

    { path: "/checkout", element: <Checkout /> },
    { path: "/checkout/retorno", element: <PagamentoRetorno /> },

    { path: "/conta", element: <Perfil /> },
    { path: "/conta/perfil", element: <Perfil /> },
    { path: "/conta/enderecos", element: <Enderecos /> },
    { path: "/conta/pedidos", element: <MeusPedidos /> },
    { path: "/conta/pedidos/:uuid", element: <DetalhePedido /> },
    { path: "/conta/lista-desejos", element: <ListaDesejos /> },
];

export default rotasCompra;
