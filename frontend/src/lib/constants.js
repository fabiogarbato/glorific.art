/** Chaves de storage e constantes globais do front. Nada de string solta no codigo. */

/**
 * OBSOLETO. O access token nao vai mais para o storage — ele vive em memoria
 * (ver api/client.js). A chave fica so para limpar sobra de instalacao antiga;
 * nao volte a gravar token aqui.
 */
export const TOKEN_KEY = "glorific_token";
export const CARRINHO_KEY = "glorific_cart_v1";

/**
 * Marca de "este navegador ja teve sessao aqui". NAO e credencial — e so uma
 * dica para o AuthProvider nao disparar um /auth/refresh (e um 401 garantido)
 * no carregamento de cada visitante anonimo da vitrine.
 */
export const SESSAO_KEY = "glorific_sessao";

/** Carrinho anonimo expira em 7 dias (mesma janela do repo de referencia). */
export const CARRINHO_TTL_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * Uuid do checkout em andamento.
 *
 * O backend redireciona o cliente de volta da InfinitePay para
 * `/checkout/retorno?resultado=...` e NAO devolve o pedido na URL (de proposito:
 * nada de dado de cliente em query string). Guardamos o uuid aqui na ida para
 * saber qual pedido consultar na volta. `sessionStorage` porque isso morre com a
 * aba — nao e estado de longo prazo.
 */
export const CHECKOUT_UUID_KEY = "glorific_checkout_uuid";

/** Claims .NET chegam ora com nome curto, ora com a URI longa. */
export const CLAIM = {
    role: "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
    email: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
    nameId: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
    telefone: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone",
};

/**
 * Espelho de `Glorific.Domain.Constants.Roles`. Minusculo e sem espaco: e
 * exatamente o valor que sai na claim `role` do JWT.
 */
export const ROLES = {
    ADMIN: "admin",
    GERENTE: "gerente",
    OPERADOR: "operador",
    CLIENTE: "cliente",
};

/**
 * Papeis que abrem o painel administrativo (mesma lista de
 * `Roles.Administrativos` no backend). Serve apenas para a UI decidir o que
 * mostrar — a autorizacao de verdade e a policy do servidor.
 */
export const PAPEIS_ADMINISTRATIVOS = [ROLES.ADMIN, ROLES.GERENTE, ROLES.OPERADOR];

/**
 * Limites de senha do backend (`Senhas.MaximoBytes` e o `StringLength` dos DTOs).
 * O teto e 72 porque acima disso o BCrypt IGNORA o resto — aceitar mais no front
 * seria prometer uma forca que o hash nao tem.
 */
export const SENHA = { MIN: 8, MAX: 72 };

export const ITENS_POR_PAGINA = 15;
export const ITENS_POR_PAGINA_OPTIONS = [10, 25, 50];
