import { GoogleOAuthProvider } from "@react-oauth/google";
import { clientIdGoogle, googleHabilitado } from "@/lib/google.js";

/**
 * Provider do Google Identity Services — montado UMA vez, na raiz (main.jsx).
 *
 * Ele nao desenha nada: carrega o script `gsi/client` e publica o contexto que o
 * `<GoogleLogin>` consome. Por isso o lugar dele e a raiz, e nao dentro do
 * botao. Provider dentro do botao remonta a cada render da tela de sessao (um
 * caractere digitado no campo de e-mail ja basta), e cada remontagem reinicia a
 * inicializacao do GSI: o widget pisca, as vezes some, e o `credential` chega
 * atrasado ou nao chega. Aqui em cima ele inicializa uma vez por carga da
 * pagina e fica quieto.
 *
 * Sem `VITE_GOOGLE_CLIENT_ID` o provider NAO e montado — a arvore passa direta.
 * O script do Google reclama de client id vazio e derrubaria a tela inteira;
 * ficar sem o atalho social e o lado barato do erro.
 */
export default function ProvedorGoogle({ children }) {
    if (!googleHabilitado()) return children;

    return <GoogleOAuthProvider clientId={clientIdGoogle()}>{children}</GoogleOAuthProvider>;
}
