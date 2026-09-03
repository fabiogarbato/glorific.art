/**
 * Area logada do cliente: perfil e enderecos (`/api/v1/conta`).
 *
 * Nenhuma rota daqui recebe o id do dono — ele sai sempre da claim do token.
 * Endereco que existe mas e de outra pessoa responde 404 (nao 403: 403
 * confirmaria que aquele id existe).
 *
 * CEP trafega SO COM DIGITOS, como fica gravado e como o frete e cotado. A
 * mascara e assunto da tela.
 */
import api from "@/api/client.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";
import { onlyDigits } from "@/utils/masks.js";

/**
 * Limpa mascara antes de sair do navegador e normaliza os opcionais para `null`
 * (string vazia num campo com `StringLength` vira 400 no backend sem motivo).
 */
function prepararEndereco(dto) {
    const limpo = (v) => {
        const t = String(v ?? "").trim();
        return t === "" ? null : t;
    };

    return {
        apelido: limpo(dto?.apelido),
        destinatario: String(dto?.destinatario ?? "").trim(),
        documentoDestinatario: onlyDigits(dto?.documentoDestinatario) || null,
        telefoneContato: onlyDigits(dto?.telefoneContato),
        cep: onlyDigits(dto?.cep),
        logradouro: String(dto?.logradouro ?? "").trim(),
        numero: String(dto?.numero ?? "").trim(),
        complemento: limpo(dto?.complemento),
        bairro: String(dto?.bairro ?? "").trim(),
        cidade: String(dto?.cidade ?? "").trim(),
        uf: String(dto?.uf ?? "").trim().toUpperCase(),
    };
}

export const contaService = {
    // ---------------------------------------------------------------- perfil

    // GET /api/v1/conta
    async obterPerfil() {
        const { data } = await api.get("/conta");
        return data ?? null;
    },

    /**
     * PUT /api/v1/conta — e-mail nao entra: trocar e-mail exige reverificacao e tem
     * fluxo proprio no backend.
     */
    async atualizarPerfil(dto) {
        const corpo = {
            nomeCompleto: String(dto?.nomeCompleto ?? "").trim(),
            telefone: onlyDigits(dto?.telefone) || null,
            cpf: onlyDigits(dto?.cpf) || null,
            dataNascimento: dto?.dataNascimento || null,
            aceitaMarketing: !!dto?.aceitaMarketing,
        };

        const { data } = await api.put("/conta", corpo);
        return data ?? null;
    },

    // ------------------------------------------------------------- enderecos

    // GET /api/v1/conta/enderecos
    async listarEnderecos() {
        const { data } = await api.get("/conta/enderecos");
        return Array.isArray(data) ? data : [];
    },

    // GET /api/v1/conta/enderecos/{id}
    async obterEndereco(id) {
        try {
            const { data } = await api.get(`/conta/enderecos/${id}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    // POST /api/v1/conta/enderecos — 201
    async criarEndereco(dto) {
        const { data } = await api.post("/conta/enderecos", {
            ...prepararEndereco(dto),
            principal: !!dto?.principal,
        });
        return data ?? null;
    },

    /**
     * PUT /api/v1/conta/enderecos/{id} — `principal` fica de fora de proposito:
     * promover tem endpoint proprio porque o efeito e sobre os OUTROS enderecos.
     */
    async atualizarEndereco(id, dto) {
        const { data } = await api.put(`/conta/enderecos/${id}`, prepararEndereco(dto));
        return data ?? null;
    },

    // DELETE /api/v1/conta/enderecos/{id} — 204, sem corpo.
    async removerEndereco(id) {
        await api.delete(`/conta/enderecos/${id}`);
    },

    // PUT /api/v1/conta/enderecos/{id}/principal — so pode existir um por cliente.
    async definirPrincipal(id) {
        const { data } = await api.put(`/conta/enderecos/${id}/principal`);
        return data ?? null;
    },
};

export default contaService;
