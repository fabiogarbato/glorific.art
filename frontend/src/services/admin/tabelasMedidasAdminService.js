import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Guias de medidas. Fonte: `Admin/TabelasMedidasAdminController.cs` — CRUD
 * generico puro.
 *
 * Regra do backend que a tela precisa respeitar: as linhas enviadas SUBSTITUEM
 * as atuais em bloco. Nao existe PATCH de linha; quem edita uma medida reenvia
 * a tabela inteira.
 */
const BASE = "/admin/tabelas-medidas";

export const tabelasMedidasAdminService = criarServicoCrud(BASE);

export default tabelasMedidasAdminService;
