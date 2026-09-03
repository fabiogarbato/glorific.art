import api from "@/api/client.js";
import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Categorias do painel. Fonte: `Admin/CategoriasAdminController.cs`
 * (GenericController + a rota `arvore`).
 */
const BASE = "/admin/categorias";

const crud = criarServicoCrud(BASE);

export const categoriasAdminService = {
    ...crud,

    /**
     * GET /api/v1/admin/categorias/arvore?somenteHabilitadas=
     *
     * Arvore completa e SEM paginacao — a auto-relacao e de um nivel so.
     * E daqui que sai o seletor de "categoria pai" e o filtro do formulario de
     * produto, porque o painel edita o que a loja esconde.
     */
    async arvore(somenteHabilitadas = false) {
        const { data } = await api.get(`${BASE}/arvore`, { params: { somenteHabilitadas } });
        return Array.isArray(data) ? data : [];
    },
};

/** Achata a arvore em `[{ ...categoria, profundidade }]` para alimentar um `<select>`. */
export function achatarArvore(arvore = [], profundidade = 0) {
    return arvore.flatMap((categoria) => [
        { ...categoria, profundidade },
        ...achatarArvore(categoria.filhas ?? [], profundidade + 1),
    ]);
}

export default categoriasAdminService;
