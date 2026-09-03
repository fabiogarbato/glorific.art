import api from "@/api/client.js";
import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Grade de tamanhos. Fonte: `Admin/TamanhosAdminController.cs`.
 * A coluna `ordem` e o que faz o seletor sair PP, P, M, G, GG.
 */
const BASE = "/admin/tamanhos";

const crud = criarServicoCrud(BASE);

export const tamanhosAdminService = {
    ...crud,

    /**
     * GET /api/v1/admin/tamanhos/ativos?grade=
     * Lista curta, ja ordenada, sem paginacao — alimenta a matriz de variacoes.
     * `grade` e o enum `GradeTamanho` (1 Alfa, 2 Numerica, 3 Unico, 4 Infantil).
     */
    async ativos(grade = null) {
        const { data } = await api.get(`${BASE}/ativos`, {
            params: grade ? { grade } : undefined,
        });
        return Array.isArray(data) ? data : [];
    },
};

export default tamanhosAdminService;
