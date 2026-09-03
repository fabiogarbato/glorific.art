/**
 * Tudo que o front precisa saber sobre o login com Google, num lugar so.
 *
 * Duas responsabilidades, e nenhuma delas e "autenticar": quem valida o
 * id_token contra o JWKS do Google e o backend, em POST /auth/google.
 *
 * 1. CONFIGURACAO. O client id sai de `VITE_GOOGLE_CLIENT_ID`. Sem ele o
 *    provider nao e montado e o botao some — renderizar o widget do Google sem
 *    client id derruba a tela de sessao inteira com erro do script deles, e a
 *    pessoa fica sem conseguir entrar nem por e-mail e senha. Perder o atalho e
 *    muito mais barato do que perder a tela.
 *
 *    Sao FUNCOES, e nao constantes de modulo, de proposito: constante congela o
 *    valor no primeiro import, o que impede o teste de exercitar os dois estados
 *    (com e sem client id) no mesmo arquivo. O Vite substitui
 *    `import.meta.env.VITE_*` por literal em qualquer posicao, entao ler dentro
 *    da funcao nao custa nada em producao.
 *
 * 2. TRADUCAO DE ERRO. A falha do Google precisa virar frase legivel ao lado do
 *    formulario. O envelope do backend (`{ statusCode, error, traceId }`) nao
 *    serve cru: o 401 e generico de proposito ("Autenticacao necessaria ou
 *    credencial invalida"), o 500 do Google nao configurado sai como "erro
 *    inesperado" e as mensagens de regra de negocio vem sem acento. Aqui cada
 *    caso vira uma frase que diz a pessoa o que aconteceu e o que fazer.
 */
import { getApiError } from "@/utils/apiError.js";

/** Client id do Google Identity Services, ja sem espaco em volta. */
export function clientIdGoogle() {
    return String(import.meta.env.VITE_GOOGLE_CLIENT_ID ?? "").trim();
}

/** `false` = o bloco do Google nao existe na interface. E um estado valido. */
export function googleHabilitado() {
    return clientIdGoogle() !== "";
}

/**
 * Regras de negocio do backend (HTTP 400) reescritas em portugues de loja.
 *
 * O casamento e por trecho, e nao por igualdade: as mensagens do servidor sao
 * escritas sem acento e podem mudar de redacao sem aviso — travar no texto
 * exato faria a traducao falhar em silencio no dia da mudanca.
 */
const REGRAS = [
    {
        padrao: /desativad|inativ/i,
        texto: "Esta conta está desativada. Fale com o atendimento para reativá-la.",
    },
    {
        padrao: /n[ãa]o verificado/i,
        texto:
            "O Google não confirmou o e-mail desta conta. Verifique o endereço na sua conta Google e tente de novo.",
    },
    {
        padrao: /dom[íi]nio/i,
        texto: "Esta conta Google não pertence a um domínio autorizado nesta loja.",
    },
    {
        padrao: /vinculada/i,
        texto: "Esta conta Google já está vinculada a outro cadastro.",
    },
    {
        padrao: /sem e-?mail|sem identificador/i,
        texto:
            "O Google não enviou os dados necessários para identificar você. Tente entrar com e-mail e senha.",
    },
];

/**
 * Erro do fluxo Google -> frase para a tela. Nunca devolve string vazia: a
 * pessoa precisa ver ALGUMA explicacao ao lado do formulario.
 */
export function mensagemErroGoogle(err) {
    // Falha antes de existir resposta (rede fora, timeout, DNS): nao ha o que
    // interpretar, e insistir no mesmo botao nao resolve.
    if (!err?.response) {
        return "Não foi possível falar com o servidor. Verifique sua conexão e tente novamente.";
    }

    const { status, message } = getApiError(err);

    // O 401 do backend e deliberadamente generico — detalhar por que a
    // credencial falhou ajudaria quem esta adivinhando. Aqui ele vira a unica
    // leitura possivel: o id_token nao serve mais.
    if (status === 401) {
        return "O Google não confirmou esta identidade. Tente entrar novamente.";
    }

    if (status === 403) {
        return "Esta conta não tem permissão para entrar na loja.";
    }

    // Google sem configuracao no servidor sobe como erro de operacao (500), e
    // nao como credencial invalida. Para quem esta na tela, a saida util e a
    // senha — dizer "tente de novo" seria mentira, o proximo clique falha igual.
    if (status >= 500) {
        return "O login com Google está indisponível no momento. Entre com e-mail e senha.";
    }

    const regra = REGRAS.find((r) => r.padrao.test(message ?? ""));
    if (regra) return regra.texto;

    return message || "Não foi possível entrar com o Google. Tente novamente.";
}

export default googleHabilitado;
