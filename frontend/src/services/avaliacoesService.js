import api from "@/api/client.js";

/**
 * Avaliacoes de produto — AvaliacoesController (`/api/v1/avaliacoes`).
 *
 *   GET  /api/v1/avaliacoes/produtos/{idProduto}        -> PagedResult<AvaliacaoResponseDto>
 *   GET  /api/v1/avaliacoes/produtos/{idProduto}/resumo -> AvaliacaoResumoDto
 *   POST /api/v1/avaliacoes                             -> 201 (entra PENDENTE)
 *
 * Leitura e publica; o POST exige token (o interceptor injeta o Bearer). O 201
 * significa "recebida", nao "publicada": a avaliacao passa por moderacao antes
 * de aparecer na vitrine — e a page precisa dizer isso ao cliente.
 */

const BASE = "/avaliacoes";

export const avaliacoesService = {
    async listarDoProduto(idProduto, { pagina = 1, tamanhoPagina = 5 } = {}) {
        const { data } = await api.get(`${BASE}/produtos/${idProduto}`, {
            params: { page: pagina, pageSize: tamanhoPagina },
        });

        const itens = Array.isArray(data?.items) ? data.items : [];
        const total = data?.total ?? itens.length;
        const pageSize = data?.pageSize ?? tamanhoPagina;

        return {
            itens,
            pagina: data?.page ?? pagina,
            tamanhoPagina: pageSize,
            total,
            totalPaginas: data?.totalPages ?? (pageSize > 0 ? Math.ceil(total / pageSize) : 0),
        };
    },

    /**
     * Media, distribuicao por nota, percentual de recomendacao e caimento
     * predominante. `distribuicaoPorNota` chega como objeto {"1":0,...,"5":12}:
     * as chaves viajam em texto porque JSON nao tem chave numerica.
     */
    async resumoDoProduto(idProduto) {
        const { data } = await api.get(`${BASE}/produtos/${idProduto}/resumo`);
        const distribuicao = data?.distribuicaoPorNota ?? {};

        return {
            idProduto: data?.idProduto ?? idProduto,
            notaMedia: data?.notaMedia ?? null,
            totalAvaliacoes: data?.totalAvaliacoes ?? 0,
            distribuicaoPorNota: {
                1: Number(distribuicao[1] ?? distribuicao["1"] ?? 0),
                2: Number(distribuicao[2] ?? distribuicao["2"] ?? 0),
                3: Number(distribuicao[3] ?? distribuicao["3"] ?? 0),
                4: Number(distribuicao[4] ?? distribuicao["4"] ?? 0),
                5: Number(distribuicao[5] ?? distribuicao["5"] ?? 0),
            },
            percentualRecomenda: data?.percentualRecomenda ?? null,
            caimentoPredominante: data?.caimentoPredominante ?? null,
            totalRespostasCaimento: data?.totalRespostasCaimento ?? 0,
        };
    },

    /**
     * Envia a avaliacao. Campos opcionais precisam ir como `undefined` (e nao
     * como "" ou 0) — string vazia estoura o [StringLength] e zero estoura o
     * [Range] de altura/peso no DTO.
     */
    async criar({
        idProduto,
        nota,
        titulo,
        comentario,
        tamanhoComprado,
        alturaClienteCm,
        pesoClienteKg,
        caimento,
        recomenda,
        idPedidoItem,
    }) {
        const { data } = await api.post(BASE, {
            idProduto,
            nota,
            titulo: titulo?.trim() || undefined,
            comentario: comentario?.trim() || undefined,
            tamanhoComprado: tamanhoComprado || undefined,
            alturaClienteCm: alturaClienteCm || undefined,
            pesoClienteKg: pesoClienteKg || undefined,
            caimento: caimento || undefined,
            recomenda: typeof recomenda === "boolean" ? recomenda : undefined,
            idPedidoItem: idPedidoItem || undefined,
        });

        return data ?? null;
    },
};

export default avaliacoesService;
