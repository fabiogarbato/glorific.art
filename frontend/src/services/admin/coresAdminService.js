import api from "@/api/client.js";
import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Cores e swatches. Fonte: `Admin/CoresAdminController.cs`.
 * `hexRgb` obedece ao regex `^#[0-9a-fA-F]{6}$` no backend — o front pinta a
 * bolinha direto com esse valor.
 */
const BASE = "/admin/cores";

const crud = criarServicoCrud(BASE);

export const coresAdminService = {
    ...crud,

    /** GET /api/v1/admin/cores/ativas — lista curta e ordenada, sem paginacao. */
    async ativas() {
        const { data } = await api.get(`${BASE}/ativas`);
        return Array.isArray(data) ? data : [];
    },
};

export default coresAdminService;
