import api from "@/api/client.js";
import { normalizarPagina } from "@/lib/pagedResult.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

const BASE = "/admin/usuarios";

/**
 * UsuariosAdminController — /api/v1/admin/usuarios (policy SomenteAdmin).
 *
 * Papel NAO entra no PUT: conceder e revogar tem endpoint proprio porque sao as
 * operacoes mais perigosas do sistema e precisam ficar auditaveis. Revogar
 * derruba as sessoes do alvo para o privilegio nao sobreviver no token antigo.
 *
 * O servico tambem recusa que alguem altere os proprios papeis ou desative a
 * propria conta — a tela antecipa isso desabilitando o controle, mas a trava de
 * verdade e a do servidor.
 */
export const usuariosAdminService = {
    async listar({ search, papel, ativo, page, pageSize } = {}) {
        const { data } = await api.get(BASE, {
            params: {
                search: search || undefined,
                papel: papel || undefined,
                ativo: ativo === "" || ativo == null ? undefined : ativo,
                page,
                pageSize,
            },
        });
        return normalizarPagina(data, pageSize);
    },

    async obter(id) {
        try {
            const { data } = await api.get(`${BASE}/${id}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    async atualizar(id, { nomeCompleto, telefone, cpf, aceitaMarketing }) {
        const { data } = await api.put(`${BASE}/${id}`, {
            nomeCompleto: nomeCompleto || null,
            telefone: telefone ? telefone.replace(/\D/g, "") : null,
            cpf: cpf ? cpf.replace(/\D/g, "") : null,
            aceitaMarketing: !!aceitaMarketing,
        });
        return data;
    },

    /** POST /{id}/roles/{papel} — idempotente. */
    async concederPapel(id, papel) {
        const { data } = await api.post(`${BASE}/${id}/roles/${papel}`);
        return data;
    },

    /** DELETE /{id}/roles/{papel} — revoga e derruba as sessoes do alvo. */
    async revogarPapel(id, papel) {
        const { data } = await api.delete(`${BASE}/${id}/roles/${papel}`);
        return data;
    },

    /** POST /{id}/desativar — soft delete + revogacao de todas as sessoes. */
    async desativar(id) {
        const { data } = await api.post(`${BASE}/${id}/desativar`);
        return data;
    },

    async ativar(id) {
        const { data } = await api.post(`${BASE}/${id}/ativar`);
        return data;
    },
};

export default usuariosAdminService;
