import Login from "@/pages/Login.jsx";
import Cadastro from "@/pages/Cadastro.jsx";
import EsqueciSenha from "@/pages/EsqueciSenha.jsx";
import RedefinirSenha from "@/pages/RedefinirSenha.jsx";

/**
 * Rotas de sessao, para o integrador compor em `routes/index.jsx`.
 *
 * Todas sao publicas — inclusive `/redefinir-senha`, que e aberta com o token do
 * e-mail justamente por quem NAO consegue entrar. Exigir sessao ali seria pedir
 * a senha que a pessoa esta tentando recuperar.
 *
 * Elas pertencem ao chassi da loja (`LayoutLoja`): quem esta entrando continua
 * vendo cabecalho e rodape, e nao cai numa tela ilhada.
 *
 * `/recuperar-senha` fica como apelido de `/esqueci-senha` porque o link antigo
 * ja circula na interface; um endereco que morre em 404 e pior do que um apelido.
 * O caminho `/redefinir-senha` NAO pode mudar: e o que
 * `AutenticacaoService.EsqueciSenhaAsync` escreve no e-mail
 * (`/redefinir-senha?token=...`).
 */
const rotasAuth = [
    { path: "/login", element: <Login />, publica: true },
    { path: "/cadastro", element: <Cadastro />, publica: true },
    { path: "/esqueci-senha", element: <EsqueciSenha />, publica: true },
    { path: "/recuperar-senha", element: <EsqueciSenha />, publica: true },
    { path: "/redefinir-senha", element: <RedefinirSenha />, publica: true },
];

export default rotasAuth;
