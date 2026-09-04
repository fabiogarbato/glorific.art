import api from "@/api/client.js";
import { criarServicoCrud } from "./crudAdmin.js";

/**
 * Acervo de imagens do catalogo. Fonte: `Admin/MidiasAdminController.cs`.
 *
 * O upload e um endpoint proprio, multipart — nao o POST do CRUD generico, que
 * espera JSON de midia JA hospedada (import de acervo).
 */
const BASE = "/admin/midias";

/** Limite de negocio do backend (o teto de requisicao e 12 MB, com folga proposital). */
export const TAMANHO_MAXIMO_BYTES = 8 * 1024 * 1024;

export const FORMATOS_ACEITOS = "image/jpeg,image/png,image/webp,image/avif";

const crud = criarServicoCrud(BASE);

export const midiasAdminService = {
    ...crud,

    /**
     * POST /api/v1/admin/midias/upload (multipart/form-data)
     * Campos: `arquivo` (IFormFile) e `altText` (opcional).
     *
     * O Content-Type precisa ser sobrescrito: o client tem
     * "application/json" como default e o axios serializaria o FormData como
     * JSON. Declarando multipart, o navegador assume e escreve o boundary.
     */
    async enviar(arquivo, altText = "") {
        const corpo = new FormData();
        corpo.append("arquivo", arquivo);
        if (altText) corpo.append("altText", altText);

        const { data } = await api.post(`${BASE}/upload`, corpo, {
            headers: { "Content-Type": "multipart/form-data" },
            timeout: 60000, // upload de 8 MB em rede ruim passa dos 20 s do default
        });
        return data;
    },

    /** PUT /api/v1/admin/midias/{id} — so o texto alternativo e editavel. */
    async atualizarAltText(id, altText) {
        const { data } = await api.put(`${BASE}/${id}`, { altText });
        return data;
    },

    /**
     * POST /api/v1/admin/midias/{id}/gerar-texto-alternativo
     * Le a propria imagem e alt texts de outras midias do acervo, devolve so o TEXTO sugerido —
     * nao salva nada. Quem grava e atualizarAltText, quando o admin confirmar/editar.
     */
    async gerarTextoAlternativo(id) {
        const { data } = await api.post(`${BASE}/${id}/gerar-texto-alternativo`, null, {
            timeout: 70000, // a OpenAI le a imagem e pode passar do timeout padrao (20s)
        });
        return data?.descricao ?? '';
    },
};

export default midiasAdminService;
