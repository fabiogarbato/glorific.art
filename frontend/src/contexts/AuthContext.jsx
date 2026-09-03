import { createContext, useCallback, useEffect, useMemo, useRef, useState } from "react";
import api, {
    getToken,
    setToken,
    limparSessao,
    lerPayloadJwt,
    registrarTratadorDeNaoAutorizado,
} from "@/api/client.js";
import { authService } from "@/services/authService.js";
import { CLAIM, PAPEIS_ADMINISTRATIVOS, ROLES, SESSAO_KEY } from "@/lib/constants.js";

export const AuthContext = createContext(null);

/**
 * Sessao real do front.
 *
 * O desenho, em uma frase: o access token (15 min) vive em memoria e a
 * continuidade vem do refresh token, que so o navegador enxerga, num cookie
 * httpOnly com `Path=/api/v1/auth`.
 *
 * Disso saem as tres unicas coisas que este provider faz:
 *
 * - RESTAURA: um F5 comeca sem token nenhum, entao a montagem tenta um refresh
 *   silencioso antes de decidir se ha sessao. Enquanto isso `inicializando` e
 *   verdadeiro e os guards de rota esperam — sem isso todo reload em /conta
 *   jogaria a pessoa no login.
 * - AGENDA: renova sozinho pouco antes do `exp` do JWT, para a sessao nao morrer
 *   no meio de um checkout.
 * - REAGE: um 401 ganha UMA tentativa de renovacao e o request original e
 *   repetido. Uma so, e nunca partindo do proprio /refresh, que e onde moram os
 *   lacos infinitos.
 */

/** Renova esta folga antes do vencimento — cobre latencia e relogio torto. */
export const FOLGA_RENOVACAO_MS = 60_000;

/** Nunca agenda mais longe que isto: `setTimeout` longo demais e impreciso. */
const ATRASO_MAXIMO_MS = 10 * 60_000;

/**
 * Freio de rajada. Token que ja chega vencido faria o agendamento renovar em
 * sequencia, para sempre. Depois disto o provider para e deixa o proximo 401
 * resolver — uma sessao que cai e ruim, uma rajada de requisicoes e pior.
 */
const MAXIMO_RENOVACOES_IMEDIATAS = 3;

/**
 * Quanto falta para renovar, em ms, a partir do `exp` (em SEGUNDOS) do JWT.
 *
 * `null` = nao ha o que agendar (sem token ou sem `exp`).
 * `0`    = renova agora (ja venceu, ou esta dentro da folga).
 */
export function msAteRenovar(expEmSegundos, agora = Date.now()) {
    const exp = Number(expEmSegundos);
    if (!Number.isFinite(exp) || exp <= 0) return null;

    const restante = exp * 1000 - agora;
    if (restante <= FOLGA_RENOVACAO_MS) return 0;

    return Math.min(restante - FOLGA_RENOVACAO_MS, ATRASO_MAXIMO_MS);
}

/**
 * Marca de sessao anterior neste navegador.
 *
 * Sem ela, TODO visitante anonimo da vitrine dispararia um /auth/refresh no
 * carregamento so para tomar 401. Guardar isto e seguro porque nao e credencial
 * nenhuma: e um booleano que, no maximo, provoca uma tentativa de renovacao que
 * o servidor recusa. Storage bloqueado (aba anonima) devolve `true` de proposito
 * — melhor uma requisicao a mais do que uma sessao que nao volta depois do F5.
 */
function jaTeveSessao() {
    try {
        return localStorage.getItem(SESSAO_KEY) === "1";
    } catch {
        return true;
    }
}

function anotarSessao(teve) {
    try {
        if (teve) localStorage.setItem(SESSAO_KEY, "1");
        else localStorage.removeItem(SESSAO_KEY);
    } catch {
        /* storage bloqueado — segue sem a dica */
    }
}

/** Um papel administrativo em qualquer posicao da lista ja abre o painel. */
export function ehAdministrativo(papeis) {
    return (papeis ?? []).some((papel) => PAPEIS_ADMINISTRATIVOS.includes(papel));
}

/**
 * Papeis do payload do JWT.
 *
 * O backend emite UMA claim `role` por papel; o `JwtPayload` agrupa as repetidas
 * num array, mas com um papel so ela volta como string simples. Os dois formatos
 * chegam aqui, e a URI longa de schema do .NET tambem — o `OutboundClaimTypeMap`
 * esta limpo no servidor, mas um proxy ou um token antigo ainda pode trazer.
 */
export function extrairPapeisDoPayload(payload) {
    const bruto = payload?.role ?? payload?.roles ?? payload?.[CLAIM.role] ?? [];
    const lista = Array.isArray(bruto) ? bruto : [bruto];

    return lista
        .filter((papel) => typeof papel === "string" && papel.trim() !== "")
        .map((papel) => papel.trim().toLowerCase());
}

/**
 * Identidade minima lida do proprio token. Nao valida assinatura — isso e do
 * servidor; aqui o payload serve so para a interface saber o que mostrar.
 */
export function extrairUsuarioDoToken(token) {
    const p = token ? lerPayloadJwt(token) : null;
    if (!p) return null;

    const papeis = extrairPapeisDoPayload(p);

    return {
        // `sub` carrega o uuid publico. O id inteiro nunca entra no token.
        id: p.sub || p.nameid || p[CLAIM.nameId] || null,
        uuid: p.sub || p.uuid || null,
        nome: p.name || p.nome || p.unique_name || "",
        email: p.email || p[CLAIM.email] || "",
        telefone: p.telefone || p[CLAIM.telefone] || "",
        papeis,
        role: papeis[0] ?? ROLES.CLIENTE,
        isAdmin: ehAdministrativo(papeis),
        /** Familia do refresh, ou seja, a sessao. Util em log de incidente. */
        sid: p.sid ?? null,
        exp: p.exp ?? null,
    };
}

/**
 * Usuario da interface = perfil do backend (`UsuarioResponseDto`) com o token
 * como rede de seguranca. O DTO e mais rico (nome completo, papeis do banco,
 * temSenha); o token cobre o instante em que a sessao existe mas o perfil ainda
 * nao chegou.
 */
export function montarUsuario(dto, token) {
    const doToken = extrairUsuarioDoToken(token);
    if (!dto && !doToken) return null;

    const papeisDto = Array.isArray(dto?.roles)
        ? dto.roles.filter(Boolean).map((papel) => String(papel).trim().toLowerCase())
        : [];
    const papeis = papeisDto.length ? papeisDto : (doToken?.papeis ?? []);
    const administrativo = ehAdministrativo(papeis);

    return {
        id: dto?.id ?? null,
        uuid: dto?.uuid || doToken?.uuid || null,
        nome: dto?.nomeCompleto || doToken?.nome || "",
        email: dto?.email || doToken?.email || "",
        telefone: dto?.telefone || doToken?.telefone || "",
        cpf: dto?.cpf ?? "",
        fotoUrl: dto?.fotoUrl ?? null,
        dataNascimento: dto?.dataNascimento ?? null,
        emailVerificado: dto?.emailVerificado ?? false,
        aceitaMarketing: dto?.aceitaMarketing ?? false,
        /** Falso para quem entrou so por Google: a tela esconde a troca de senha. */
        temSenha: dto?.temSenha ?? true,
        googleVinculado: dto?.googleVinculado ?? false,
        papeis,
        role: papeis[0] ?? ROLES.CLIENTE,
        administrativo,
        /** Atalho historico da interface. Hoje significa "ve o painel". */
        isAdmin: administrativo,
        exp: doToken?.exp ?? null,
    };
}

export function AuthProvider({ children }) {
    const [token, setTokenState] = useState(() => getToken());
    const [perfil, setPerfil] = useState(null);
    const [inicializando, setInicializando] = useState(true);

    /**
     * Muda a cada sessao aplicada. O token novo quase sempre e uma string
     * diferente (o `jti` muda), mas depender disso para reagendar seria apostar
     * num detalhe do servidor — o contador torna o reagendamento certo.
     */
    const [versaoSessao, setVersaoSessao] = useState(0);

    /** Renovacao em voo. Cinco 401 simultaneos disparam UM refresh, nao cinco. */
    const renovacaoRef = useRef(null);

    /**
     * Vale a pena tentar renovar? Some quando um refresh falha (nao ha cookie, ou
     * ele foi revogado) e volta quando alguem entra. E o que impede o visitante
     * anonimo de gerar um /refresh a cada 401 de rota protegida.
     */
    const podeRenovarRef = useRef(jaTeveSessao());

    /** Renovacoes agendadas para "agora" em sequencia — freio de rajada. */
    const imediatasRef = useRef(0);

    const aplicarSessao = useCallback((sessao) => {
        if (!sessao?.accessToken) return null;

        setToken(sessao.accessToken);
        setTokenState(sessao.accessToken);
        setPerfil(sessao.usuario ?? null);
        setVersaoSessao((v) => v + 1);
        podeRenovarRef.current = true;
        anotarSessao(true);

        return montarUsuario(sessao.usuario ?? null, sessao.accessToken);
    }, []);

    /** Encerra so do lado do navegador (o backend ja foi avisado, ou nao ha o que avisar). */
    const encerrarLocal = useCallback(() => {
        limparSessao();
        setTokenState(null);
        setPerfil(null);
    }, []);

    /**
     * Renovacao silenciosa. Devolve o usuario, ou `null` quando nao ha sessao.
     * Nunca rejeita: quem chama (agendamento, 401, montagem) trata ausencia de
     * sessao como estado normal, e nao como erro para mostrar na tela.
     */
    const renovar = useCallback(() => {
        if (!podeRenovarRef.current) return Promise.resolve(null);

        if (!renovacaoRef.current) {
            renovacaoRef.current = authService
                .renovar()
                .then((sessao) => aplicarSessao(sessao))
                .catch(() => {
                    // Cookie ausente, expirado ou revogado. O servidor ja o apagou.
                    podeRenovarRef.current = false;
                    anotarSessao(false);
                    encerrarLocal();
                    return null;
                })
                .finally(() => {
                    renovacaoRef.current = null;
                });
        }

        return renovacaoRef.current;
    }, [aplicarSessao, encerrarLocal]);

    // ------------------------------------------------------------------
    // Restauracao na montagem
    // ------------------------------------------------------------------
    useEffect(() => {
        let vivo = true;

        renovar().finally(() => {
            if (vivo) setInicializando(false);
        });

        return () => {
            vivo = false;
        };
    }, [renovar]);

    // ------------------------------------------------------------------
    // Renovacao agendada, pouco antes do exp
    // ------------------------------------------------------------------
    useEffect(() => {
        if (!token) return undefined;

        const atraso = msAteRenovar(lerPayloadJwt(token)?.exp);
        if (atraso === null) return undefined;

        if (atraso > 0) {
            imediatasRef.current = 0;
        } else if (++imediatasRef.current > MAXIMO_RENOVACOES_IMEDIATAS) {
            // Chegou token que ja nasce dentro da folga, varias vezes seguidas:
            // ou o relogio da maquina esta muito errado, ou o servidor esta
            // emitindo token curto demais. Renovar de novo so faria uma rajada
            // de requisicoes — melhor parar e deixar o proximo 401 decidir.
            return undefined;
        }

        const id = setTimeout(() => {
            renovar();
        }, atraso);

        return () => clearTimeout(id);
    }, [token, versaoSessao, renovar]);

    // ------------------------------------------------------------------
    // 401: uma tentativa de renovacao, depois repete o request original
    // ------------------------------------------------------------------
    useEffect(
        () =>
            registrarTratadorDeNaoAutorizado(async (erro) => {
                const config = erro?.config;

                // `__semRenovar` marca as rotas de entrada (login, registro,
                // google, refresh, logout). 401 ali e credencial invalida, e a
                // tela que chamou mostra a mensagem — NAO se encerra a sessao:
                // quem ja estava logado e errou a senha noutro formulario nao
                // pode ser deslogado por isso.
                if (config?.__semRenovar) return Promise.reject(erro);

                // `__jaRenovou` garante UMA tentativa por request.
                if (!config || config.__jaRenovou || !podeRenovarRef.current) {
                    encerrarLocal();
                    return Promise.reject(erro);
                }

                const usuario = await renovar();
                if (!usuario) return Promise.reject(erro);

                config.__jaRenovou = true;
                // O header antigo carrega o token morto; o interceptor de request
                // poe o novo. Apagar aqui evita reenviar o vencido se algo falhar.
                if (config.headers) delete config.headers.Authorization;

                return api.request(config);
            }),
        [renovar, encerrarLocal],
    );

    // ------------------------------------------------------------------
    // Acoes
    // ------------------------------------------------------------------
    const login = useCallback(
        async (credenciais) => aplicarSessao(await authService.login(credenciais)),
        [aplicarSessao],
    );

    const loginGoogle = useCallback(
        async (idToken) => aplicarSessao(await authService.loginGoogle(idToken)),
        [aplicarSessao],
    );

    const registrar = useCallback(
        async (dados) => aplicarSessao(await authService.registrar(dados)),
        [aplicarSessao],
    );

    /** A troca de senha revoga tudo e devolve sessao nova — aplicar e obrigatorio. */
    const trocarSenha = useCallback(
        async (dados) => aplicarSessao(await authService.trocarSenha(dados)),
        [aplicarSessao],
    );

    /**
     * `logout()` ou `logout(true)` (compatibilidade) ou `logout({ redirecionar })`.
     * Avisa o servidor para revogar a familia do refresh; se a chamada falhar,
     * sair localmente mesmo assim — travar o usuario numa sessao que ele pediu
     * para encerrar seria o pior desfecho possivel.
     */
    const logout = useCallback(
        async (opcoes) => {
            const redirecionar = opcoes === true || opcoes?.redirecionar === true;

            try {
                await authService.logout();
            } catch {
                /* rede fora: o cookie expira sozinho e o token morre com a aba */
            }

            podeRenovarRef.current = false;
            anotarSessao(false);
            encerrarLocal();

            if (redirecionar) window.location.assign("/");
        },
        [encerrarLocal],
    );

    /** Rele o perfil (depois de editar a conta, vincular Google, etc.). */
    const recarregarPerfil = useCallback(async () => {
        const dto = await authService.eu();
        setPerfil(dto);
        return dto;
    }, []);

    const usuario = useMemo(() => montarUsuario(perfil, token), [perfil, token]);

    const valor = useMemo(
        () => ({
            usuario,
            perfil,
            token,
            /** Verdadeiro ate a restauracao terminar. Guard de rota espera por isto. */
            inicializando,
            estaAutenticado: !!usuario,
            isAdmin: !!usuario?.isAdmin,
            papeis: usuario?.papeis ?? [],
            temPapel: (papel) => (usuario?.papeis ?? []).includes(String(papel).toLowerCase()),
            login,
            loginGoogle,
            registrar,
            logout,
            renovar,
            trocarSenha,
            recarregarPerfil,
            esqueciSenha: authService.esqueciSenha,
            redefinirSenha: authService.redefinirSenha,
        }),
        [
            usuario,
            perfil,
            token,
            inicializando,
            login,
            loginGoogle,
            registrar,
            logout,
            renovar,
            trocarSenha,
            recarregarPerfil,
        ],
    );

    return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

export default AuthProvider;
