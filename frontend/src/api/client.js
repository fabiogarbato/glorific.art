/**
 * Instancia unica de axios do projeto.
 *
 * REGRA DURA: nenhum `fetch` cru e nenhum `axios.create` fora daqui.
 * A cadeia e sempre page -> hook -> service -> este client.
 */
import axios from "axios";
import { toastBus } from "./toastBus.js";

/**
 * Em dev o default cai no proxy do Vite (vite.config.js -> localhost:5080).
 *
 * A VERSAO ENTRA NA BASE, e nao no caminho de cada service: todo controller do
 * backend esta em `[Route("api/v1/...")]`, entao o service escreve `/auth/login`
 * e nunca `/v1/auth/login`. Com a base em "/api" a chamada vira /api/auth/login
 * e o servidor devolve 404 — foi assim que este arquivo comecou.
 */
const baseURL = import.meta.env.VITE_API_URL || "/api/v1";

export const api = axios.create({
    baseURL,
    timeout: 20000,
    headers: { "Content-Type": "application/json" },
});

/**
 * O access token vive EM MEMORIA — nunca em localStorage.
 *
 * Ele dura 15 minutos e a continuidade da sessao vem do refresh token, que o
 * backend entrega num cookie httpOnly (ver CookieRefresh.cs). Guardar o access
 * token em storage legivel por script daria a um unico XSS material para
 * trabalhar; em memoria, o pior caso morre junto com a aba.
 *
 * O preco e que um F5 comeca sem token — quem paga esse preco e o
 * `AuthProvider`, que faz um refresh silencioso na montagem antes de decidir se
 * a pessoa esta logada.
 */
let tokenEmMemoria = null;

/** Le o token sem depender de React (o interceptor roda fora da arvore). */
export function getToken() {
    return tokenEmMemoria;
}

export function setToken(token) {
    tokenEmMemoria = token || null;
}

export function limparSessao() {
    setToken(null);
}

/** Decodifica o payload do JWT sem validar assinatura (isso e papel do backend). */
export function lerPayloadJwt(token) {
    try {
        let base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
        // base64url do JWT vem sem padding; atob exige multiplo de 4.
        base64 = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
        const json = decodeURIComponent(
            atob(base64)
                .split("")
                .map((c) => "%" + c.charCodeAt(0).toString(16).padStart(2, "0"))
                .join(""),
        );
        return JSON.parse(json);
    } catch {
        return null;
    }
}

/** true quando o token ainda vale (com 5 s de folga para latencia de rede). */
export function tokenValido(token) {
    const payload = token ? lerPayloadJwt(token) : null;
    if (!payload?.exp) return false;
    return payload.exp * 1000 > Date.now() + 5000;
}

// ---------------------------------------------------------------------------
// 1) REQUEST — injeta o Bearer apenas se o `exp` do JWT ainda e futuro.
//    Token vencido e descartado aqui mesmo: evita um round-trip garantido de 401.
// ---------------------------------------------------------------------------
api.interceptors.request.use(
    (config) => {
        const token = getToken();
        if (token) {
            if (tokenValido(token)) {
                config.headers.Authorization = `Bearer ${token}`;
            } else {
                limparSessao();
            }
        }
        return config;
    },
    (error) => Promise.reject(error),
);

// ---------------------------------------------------------------------------
// Ponto de extensao do 401.
//
// O `AuthProvider` registra aqui a tentativa de renovacao silenciosa: sem ele o
// interceptor mantem o comportamento antigo (limpa a sessao e volta ao login).
// Um handler so, registrado por quem e dono da sessao — nada de cada service
// inventar a propria politica de 401.
// ---------------------------------------------------------------------------
let tratadorDeNaoAutorizado = null;

/** @param {(erro: import('axios').AxiosError) => Promise<unknown>} fn */
export function registrarTratadorDeNaoAutorizado(fn) {
    tratadorDeNaoAutorizado = fn;
    return () => {
        if (tratadorDeNaoAutorizado === fn) tratadorDeNaoAutorizado = null;
    };
}

// ---------------------------------------------------------------------------
// 2) RESPONSE — 401 limpa a sessao e volta ao login; 404 e silencioso (o service
//    decide se e estado normal); qualquer outro erro vira toast global.
// ---------------------------------------------------------------------------
api.interceptors.response.use(
    (response) => response,
    (error) => {
        const status = error.response?.status;

        if (status === 401) {
            // 401 nunca vira toast: ou o dono da sessao renova, ou o guard de
            // rota manda para o login. Toast aqui so duplicaria a mensagem.
            if (tratadorDeNaoAutorizado) {
                return tratadorDeNaoAutorizado(error);
            }

            limparSessao();
            // Nao redireciona a partir da tela de login (o form mostra o proprio erro).
            if (!window.location.pathname.startsWith("/login")) {
                window.location.assign("/login");
            }
            return Promise.reject(error);
        }

        // `__semToast` e o pedido de silencio de quem MOSTRA o proprio erro.
        // Vale para as rotas de entrada (login com Google, por exemplo): a
        // pessoa precisa ler "esta conta esta desativada" ao lado do botao que
        // ela acabou de clicar, e nao num aviso flutuante no canto da tela que
        // some sozinho em tres segundos.
        if (error.config?.__semToast) return Promise.reject(error);

        if (status === 403) {
            toastBus.emit("Você não tem permissão para essa ação.", "error");
            return Promise.reject(error);
        }

        // 404 e estado de dominio ("ainda nao existe"), nao falha de sistema.
        if (status !== 404) {
            // Envelope oficial do backend: { statusCode, error, traceId }.
            // `detail`/`title` cobrem ProblemDetails de outras fontes.
            const mensagem =
                error.response?.data?.error ||
                error.response?.data?.detail ||
                error.response?.data?.title ||
                (error.code === "ECONNABORTED"
                    ? "A requisição demorou demais. Tente novamente."
                    : null) ||
                (!error.response ? "Não foi possível falar com o servidor." : null) ||
                // 5xx sem envelope é servidor fora do ar (ou o proxy do Vite sem
                // ninguém atrás). "Erro inesperado" faria a pessoa procurar culpa
                // no que ela digitou; o problema não é dela e não adianta refazer
                // a ação agora.
                (status >= 500
                    ? "O servidor não respondeu. Tente novamente em instantes."
                    : null) ||
                "Ocorreu um erro inesperado";
            toastBus.emit(mensagem, "error");
        }

        return Promise.reject(error);
    },
);

export default api;
