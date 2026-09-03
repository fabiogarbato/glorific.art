import api from "@/api/client.js";
import { normalizarPagina, paramsPaginacao } from "./paginacao.js";

/**
 * Fabrica das cinco actions do `GenericController<TEntity, ...>` do backend:
 *
 *   GET    {base}?page=&pageSize=   -> PagedResult<TResponse>
 *   GET    {base}/{id}              -> TResponse
 *   POST   {base}                   -> 201 TResponse
 *   PUT    {base}/{id}              -> 200 TResponse   (o id vem da ROTA)
 *   DELETE {base}/{id}              -> 204
 *
 * Importante: o CRUD generico NAO aceita busca nem ordenacao — so `page` e
 * `pageSize`. Nenhuma tela deste modulo pode prometer busca no servidor para os
 * recursos que herdam dele.
 */
export function criarServicoCrud(base) {
    return {
        base,

        async listar({ pagina = 1, tamanhoPagina } = {}) {
            const { data } = await api.get(base, { params: paramsPaginacao(pagina, tamanhoPagina) });
            return normalizarPagina(data);
        },

        async obter(id) {
            const { data } = await api.get(`${base}/${id}`);
            return data ?? null;
        },

        async criar(payload) {
            const { data } = await api.post(base, payload);
            return data;
        },

        async atualizar(id, payload) {
            const { data } = await api.put(`${base}/${id}`, payload);
            return data;
        },

        async remover(id) {
            await api.delete(`${base}/${id}`);
        },
    };
}

export default criarServicoCrud;
