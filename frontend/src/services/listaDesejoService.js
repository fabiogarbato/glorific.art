/**
 * Lista de desejos do cliente autenticado (`/api/v1/lista-desejos`).
 *
 * O dono sai sempre do token — nenhuma rota aceita id de usuario. A remocao e
 * por ID DE PRODUTO (a chave de negocio e o par usuario+produto), entao o front
 * nao precisa guardar o id da linha.
 *
 * A variacao e opcional por decisao de produto: em moda o cliente favorita a
 * peca antes de decidir o tamanho.
 */
import api from "@/api/client.js";

export const listaDesejoService = {
    // GET /api/v1/lista-desejos — lista completa, com capa, preco e disponibilidade.
    async listar() {
        const { data } = await api.get("/lista-desejos");
        return Array.isArray(data) ? data : [];
    },

    /**
     * GET /api/v1/lista-desejos/ids — so os ids de produto. E o que pinta o coracao
     * em todos os cards da vitrine sem uma requisicao por card.
     */
    async ids() {
        const { data } = await api.get("/lista-desejos/ids");
        return Array.isArray(data) ? data : [];
    },

    // POST /api/v1/lista-desejos — idempotente: favoritar o que ja esta la devolve 200.
    async adicionar({ idProduto, idVariacao = null }) {
        const { data } = await api.post("/lista-desejos", {
            idProduto: Number(idProduto),
            idVariacao: idVariacao == null ? null : Number(idVariacao),
        });
        return data ?? null;
    },

    /** POST /api/v1/lista-desejos/alternar — devolve `true` se entrou, `false` se saiu. */
    async alternar({ idProduto, idVariacao = null }) {
        const { data } = await api.post("/lista-desejos/alternar", {
            idProduto: Number(idProduto),
            idVariacao: idVariacao == null ? null : Number(idVariacao),
        });
        return data === true;
    },

    // DELETE /api/v1/lista-desejos/produtos/{idProduto} — 204, sem corpo.
    async remover(idProduto) {
        await api.delete(`/lista-desejos/produtos/${idProduto}`);
    },
};

export default listaDesejoService;
