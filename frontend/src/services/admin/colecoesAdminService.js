import api from "@/api/client.js";
import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Colecoes (drops) e a curadoria de quais pecas entram em cada uma.
 * Fonte: `Admin/ColecoesAdminController.cs`.
 */
const BASE = "/admin/colecoes";

const crud = criarServicoCrud(BASE);

export const colecoesAdminService = {
    ...crud,

    /**
     * POST /api/v1/admin/colecoes/{id}/produtos
     * Revincular o mesmo produto so muda a ordem — nao duplica o vinculo.
     */
    async vincularProduto(idColecao, { idProduto, ordem = 0 }) {
        await api.post(`${BASE}/${idColecao}/produtos`, { idProduto, ordem });
    },

    // DELETE /api/v1/admin/colecoes/{id}/produtos/{idProduto}
    async desvincularProduto(idColecao, idProduto) {
        await api.delete(`${BASE}/${idColecao}/produtos/${idProduto}`);
    },
};

export default colecoesAdminService;
