/**
 * Sessao: `AuthController` (`/api/v1/auth`).
 *
 * Tres coisas que este arquivo assume, e que vem do backend, nao de palpite:
 *
 * 1. O refresh token NAO esta no corpo de resposta nenhum. Ele entra e sai pelo
 *    cookie httpOnly `Path=/api/v1/auth`, entao toda chamada daqui vai com
 *    `withCredentials` — sem isso o navegador nao anexa o cookie quando a API
 *    mora noutro host (api.glorific.art x glorific.art) e o refresh responde 401
 *    sem explicacao aparente.
 * 2. O corpo de sessao e `AutenticacaoResponseDto`:
 *    `{ accessToken, expiresIn, tokenType, usuario }`. Nao existe `token`.
 * 3. `expiresIn` vem em SEGUNDOS e e o que o front usa para agendar a renovacao.
 *
 * O service nao mexe no token guardado: quem aplica a sessao e o `AuthContext`,
 * dono unico do estado. Assim uma chamada solta (um teste, um retry) nao
 * troca a sessao do app por efeito colateral.
 */
import api from "@/api/client.js";

/** O cookie do refresh so viaja com credenciais explicitas. */
const COM_COOKIE = { withCredentials: true };

/**
 * Porta de entrada da sessao: um 401 aqui e credencial errada, nao token
 * vencido. A flag impede o `AuthContext` de tentar renovar e transformar
 * "senha incorreta" numa ida e volta inutil ao /refresh.
 */
const ENTRADA = { withCredentials: true, __semRenovar: true };

/**
 * Entrada por Google. Alem do `__semRenovar`, pede silencio ao interceptor:
 * aqui o 400 tambem carrega recado para a pessoa ("esta conta esta desativada",
 * "dominio nao autorizado") e o 500 significa "Google nao configurado no
 * servidor". Os tres precisam aparecer na tela de sessao, traduzidos por
 * `mensagemErroGoogle`, e nao como toast generico no canto.
 */
const ENTRADA_GOOGLE = { ...ENTRADA, __semToast: true };

/** Normaliza `AutenticacaoResponseDto` e derruba resposta sem token. */
function normalizarSessao(data) {
    if (!data?.accessToken) return null;

    return {
        accessToken: data.accessToken,
        expiresIn: Number(data.expiresIn) || 0,
        tokenType: data.tokenType || "Bearer",
        usuario: data.usuario ?? null,
    };
}

export const authService = {
    // POST /auth/login  -> AutenticacaoResponseDto
    async login({ email, senha }) {
        const { data } = await api.post("/auth/login", { email, senha }, ENTRADA);
        return normalizarSessao(data);
    },

    /**
     * POST /auth/register -> AutenticacaoResponseDto (ja entra logado).
     * O papel NAO vai no corpo: quem se cadastra pela loja nasce sempre cliente.
     */
    async registrar({ email, senha, nomeCompleto, telefone, cpf, aceitaMarketing = false }) {
        const { data } = await api.post(
            "/auth/register",
            {
                email,
                senha,
                nomeCompleto,
                // Campo opcional vazio vira `null`: string vazia estoura o
                // StringLength/regex do DTO a toa.
                telefone: telefone || null,
                cpf: cpf || null,
                aceitaMarketing,
            },
            ENTRADA,
        );
        return normalizarSessao(data);
    },

    /**
     * POST /auth/google -> AutenticacaoResponseDto.
     * `idToken` e o credential do Google Identity Services; quem valida a
     * assinatura contra o JWKS do Google e o servidor.
     */
    async loginGoogle(idToken) {
        const { data } = await api.post("/auth/google", { idToken }, ENTRADA_GOOGLE);
        return normalizarSessao(data);
    },

    /**
     * POST /auth/refresh -> AutenticacaoResponseDto.
     * Sem corpo: a credencial e o cookie. `__semRenovar` e vital — um 401 aqui
     * nao pode disparar outra renovacao, que e como nasce laco infinito.
     */
    async renovar() {
        const { data } = await api.post("/auth/refresh", null, {
            withCredentials: true,
            __semRenovar: true,
        });
        return normalizarSessao(data);
    },

    /** POST /auth/logout -> 204. Revoga a familia do refresh e limpa o cookie. */
    async logout() {
        await api.post("/auth/logout", null, ENTRADA);
    },

    /** POST /auth/logout-all -> 204. Derruba a sessao em todos os dispositivos. */
    async logoutTodos() {
        await api.post("/auth/logout-all", null, COM_COOKIE);
    },

    /**
     * POST /auth/forgot-password -> 204 SEMPRE, exista a conta ou nao.
     * A tela precisa dizer a mesma coisa nos dois casos: qualquer diferenca
     * transformaria isto num verificador de quais e-mails tem conta aqui.
     */
    async esqueciSenha(email) {
        await api.post("/auth/forgot-password", { email }, ENTRADA);
    },

    /** POST /auth/reset-password -> 204. `token` e o do link do e-mail. */
    async redefinirSenha({ token, novaSenha }) {
        await api.post("/auth/reset-password", { token, novaSenha }, ENTRADA);
    },

    /**
     * POST /auth/change-password -> AutenticacaoResponseDto.
     * Devolve sessao NOVA porque a troca revoga todas as anteriores, inclusive a
     * que fez a chamada. Quem chamar precisa aplicar o token que volta.
     */
    async trocarSenha({ senhaAtual, novaSenha }) {
        const { data } = await api.post(
            "/auth/change-password",
            { senhaAtual, novaSenha },
            COM_COOKIE,
        );
        return normalizarSessao(data);
    },

    /** GET /auth/me -> UsuarioResponseDto (perfil, papeis, temSenha, googleVinculado). */
    async eu() {
        const { data } = await api.get("/auth/me", COM_COOKIE);
        return data ?? null;
    },

    /** POST /auth/link-google -> UsuarioResponseDto. Vincula Google a conta ja logada. */
    async vincularGoogle(idToken) {
        const { data } = await api.post("/auth/link-google", { idToken }, COM_COOKIE);
        return data ?? null;
    },
};

export default authService;
