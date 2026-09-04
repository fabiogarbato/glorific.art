import api from "@/api/client.js";
import { limparParams, normalizarPagina, paramsPaginacao } from "./paginacao.js";

/**
 * Painel de produtos.
 *
 * Fonte: `API/Controller/Admin/ProdutosAdminController.cs`.
 * Base do controller: `api/v1/admin/produtos`. As duas rotas de variacao por id
 * sao ABSOLUTAS no backend (`api/v1/admin/variacoes/{id}`) — por isso moram
 * neste mesmo service, mas com caminho proprio.
 *
 * O `baseURL` do axios ja inclui a versao ("/api/v1"), entao aqui os caminhos comecam direto em "/admin".
 */
const BASE = "/admin/produtos";
const BASE_VARIACOES = "/admin/variacoes";

export const produtosAdminService = {
    // ------------------------------------------------------------------ CRUD

    /**
     * GET /api/v1/admin/produtos?ativo=&categoria=&q=&page=&pageSize=
     *
     * `ativo` omitido NAO traz tudo: o controller faz `ativo ?? true` e devolve
     * so os publicados. Para ver os despublicados e preciso mandar `ativo=false`.
     */
    async listar({ ativo = true, categoria = null, q = "", pagina = 1, tamanhoPagina } = {}) {
        const { data } = await api.get(BASE, {
            params: limparParams({
                ativo,
                categoria,
                q: q?.trim() || null,
                ...paramsPaginacao(pagina, tamanhoPagina),
            }),
        });
        return normalizarPagina(data);
    },

    // GET /api/v1/admin/produtos/{id} — detalhe completo (variacoes, midias e colecoes juntas)
    async obter(id) {
        const { data } = await api.get(`${BASE}/${id}`);
        return data ?? null;
    },

    // POST /api/v1/admin/produtos
    async criar(payload) {
        const { data } = await api.post(BASE, payload);
        return data;
    },

    // PUT /api/v1/admin/produtos/{id} — o corpo nao carrega id, ele vem da rota
    async atualizar(id, payload) {
        const { data } = await api.put(`${BASE}/${id}`, payload);
        return data;
    },

    /** DELETE e SOFT: o produto continua existindo porque o historico aponta para ele. */
    async desativar(id) {
        const { data } = await api.delete(`${BASE}/${id}`);
        return data;
    },

    // POST /api/v1/admin/produtos/{id}/ativar
    async ativar(id) {
        const { data } = await api.post(`${BASE}/${id}/ativar`);
        return data;
    },

    // GET /api/v1/admin/produtos/{id}/logs — quem tirou do ar e quando
    async logs(id, { pagina = 1, tamanhoPagina } = {}) {
        const { data } = await api.get(`${BASE}/${id}/logs`, {
            params: paramsPaginacao(pagina, tamanhoPagina),
        });
        return normalizarPagina(data);
    },

    /**
     * POST /api/v1/admin/produtos/{id}/gerar-descricao
     * Le a foto de capa e descricoes de outras pecas ativas, devolve so o TEXTO sugerido —
     * nao salva nada. Quem grava e o PUT normal, quando o admin confirmar/editar.
     */
    async gerarDescricao(id) {
        const { data } = await api.post(`${BASE}/${id}/gerar-descricao`, null, {
            timeout: 70000, // a OpenAI le a imagem e pode passar do timeout padrao (20s)
        });
        return data?.descricao ?? '';
    },

    /**
     * POST /api/v1/admin/produtos/{id}/gerar-nome
     * Le a foto de capa e nomes de outras pecas ativas, devolve so o TEXTO sugerido.
     */
    async gerarNome(id) {
        const { data } = await api.post(`${BASE}/${id}/gerar-nome`, null, {
            timeout: 70000,
        });
        return data?.descricao ?? '';
    },

    /**
     * POST /api/v1/admin/produtos/{id}/gerar-sku
     * Segue o padrao de codigo das outras pecas ativas, devolve so o TEXTO sugerido.
     */
    async gerarSku(id) {
        const { data } = await api.post(`${BASE}/${id}/gerar-sku`, null, {
            timeout: 70000,
        });
        return data?.descricao ?? '';
    },

    // ------------------------------------------------------------- Variacoes

    // GET /api/v1/admin/produtos/{id}/variacoes?incluirInativas=
    async variacoes(idProduto, incluirInativas = false) {
        const { data } = await api.get(`${BASE}/${idProduto}/variacoes`, {
            params: { incluirInativas },
        });
        return Array.isArray(data) ? data : [];
    },

    /**
     * POST /api/v1/admin/produtos/{id}/variacoes
     *
     * Peso e dimensoes sao obrigatorios e POSITIVOS no DTO: o banco tem
     * CHECK (peso_gramas > 0 AND altura_cm > 0 ...) e sem eles nao ha frete.
     */
    async criarVariacao(idProduto, payload) {
        const { data } = await api.post(`${BASE}/${idProduto}/variacoes`, payload);
        return data;
    },

    /**
     * POST /api/v1/admin/produtos/{id}/variacoes/gerar-grade
     * Corpo: { idsTamanhos, idsCores, pesoGramas, alturaCm, larguraCm,
     *          comprimentoCm, precoCentavos, prefixoSku, ativo,
     *          quantidadeInicial, quantidadeMinima }
     * Resposta: { idProduto, criadas, jaExistiam, variacoes }
     * As combinacoes existentes sao preservadas como estao.
     */
    async gerarGrade(idProduto, payload) {
        const { data } = await api.post(`${BASE}/${idProduto}/variacoes/gerar-grade`, payload);
        return data;
    },

    // PUT /api/v1/admin/variacoes/{id}
    async atualizarVariacao(idVariacao, payload) {
        const { data } = await api.put(`${BASE_VARIACOES}/${idVariacao}`, payload);
        return data;
    },

    /** DELETE /api/v1/admin/variacoes/{id} — soft delete: o SKU aparece em pedido ja emitido. */
    async desativarVariacao(idVariacao) {
        await api.delete(`${BASE_VARIACOES}/${idVariacao}`);
    },

    // POST /api/v1/admin/variacoes/{id}/ativar
    async ativarVariacao(idVariacao) {
        const { data } = await api.post(`${BASE_VARIACOES}/${idVariacao}/ativar`);
        return data;
    },

    // --------------------------------------------------------------- Galeria

    // GET /api/v1/admin/produtos/{id}/midias
    async galeria(idProduto) {
        const { data } = await api.get(`${BASE}/${idProduto}/midias`);
        return Array.isArray(data) ? data : [];
    },

    /** POST /api/v1/admin/produtos/{id}/midias — vincula uma midia JA enviada. */
    async vincularMidia(idProduto, { idMidia, idCor = null, ordem = 0, ehCapa = false }) {
        const { data } = await api.post(`${BASE}/${idProduto}/midias`, {
            idMidia,
            idCor,
            ordem,
            ehCapa,
        });
        return data;
    },

    /**
     * PUT /api/v1/admin/produtos/{id}/midias/ordem
     *
     * `idsNaOrdem` sao ids da LINHA da galeria (midias_produtos), nao da midia.
     * A primeira posicao vira a capa — e assim que "marcar capa" e implementado.
     */
    async reordenarGaleria(idProduto, idsNaOrdem) {
        const { data } = await api.put(`${BASE}/${idProduto}/midias/ordem`, { idsNaOrdem });
        return Array.isArray(data) ? data : [];
    },

    /** DELETE /api/v1/admin/produtos/{id}/midias/{idMidia} — o parametro e o id da MIDIA. */
    async desvincularMidia(idProduto, idMidia) {
        await api.delete(`${BASE}/${idProduto}/midias/${idMidia}`);
    },

    /**
     * Troca a cor associada a uma foto.
     *
     * A API nao tem PUT para a linha da galeria: o vinculo carrega a cor no
     * momento em que e criado. Entao o unico caminho e desvincular e vincular de
     * novo, restaurando a ordem no fim para a foto nao pular para o final.
     */
    async alterarCorDaFoto(idProduto, itemGaleria, idCor) {
        await produtosAdminService.desvincularMidia(idProduto, itemGaleria.idMidia);

        const novo = await produtosAdminService.vincularMidia(idProduto, {
            idMidia: itemGaleria.idMidia,
            idCor: idCor ?? null,
            ordem: itemGaleria.ordem ?? 0,
            ehCapa: false,
        });

        const atual = await produtosAdminService.galeria(idProduto);

        // Recoloca a foto na posicao em que estava antes da troca.
        const semNova = atual.filter((item) => item.id !== novo.id);
        const posicao = Math.min(Math.max(itemGaleria.ordem ?? 0, 0), semNova.length);
        const ordenados = [
            ...semNova.slice(0, posicao),
            novo,
            ...semNova.slice(posicao),
        ].map((item) => item.id);

        return produtosAdminService.reordenarGaleria(idProduto, ordenados);
    },
};

export default produtosAdminService;
