import api from "@/api/client.js";
import { normalizarPagina } from "@/lib/pagedResult.js";
import { ehNaoEncontrado } from "@/utils/apiError.js";

const BASE = "/admin/cupons";

/**
 * CuponsAdminController — /api/v1/admin/cupons (policy GestaoCatalogo).
 * Herda o CRUD generico; a listagem le `search` e `ativo` da query string.
 *
 * `valor` e POLIMORFICO por tipo: percentual x100 (1250 = 12,50%) quando
 * Tipo=Percentual, centavos quando Tipo=ValorFixo, ignorado em FreteGratis.
 * A coerencia entre os dois e validada no servico — o front so nao pode
 * inverter a leitura.
 *
 * `usosAtuais` nunca vai no PUT: o contador e escrito por UPDATE condicional
 * atomico no repositorio, e mandar esse numero de volta reabriria a corrida.
 */
function montarCorpo(cupom) {
    return {
        codigo: String(cupom.codigo ?? "").trim().toUpperCase(),
        descricao: cupom.descricao || null,
        tipo: Number(cupom.tipo),
        valor: Number(cupom.valor) || 0,
        valorMinimoPedidoCentavos: cupom.valorMinimoPedidoCentavos ?? null,
        descontoMaximoCentavos: cupom.descontoMaximoCentavos ?? null,
        usoMaximoTotal: cupom.usoMaximoTotal ?? null,
        usoMaximoPorUsuario: Number(cupom.usoMaximoPorUsuario) || 1,
        vigenciaInicio: cupom.vigenciaInicio,
        vigenciaFim: cupom.vigenciaFim || null,
        primeiraCompraApenas: !!cupom.primeiraCompraApenas,
        idCategoriaRestrita: cupom.idCategoriaRestrita ?? null,
        idColecaoRestrita: cupom.idColecaoRestrita ?? null,
        ativo: !!cupom.ativo,
    };
}

export const cuponsService = {
    // GET ?search=&ativo=&page=&pageSize=
    async listar({ search, ativo, page, pageSize } = {}) {
        const { data } = await api.get(BASE, {
            params: {
                search: search || undefined,
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

    // GET /por-codigo/{codigo} — ja normalizado em maiusculas no servico
    async obterPorCodigo(codigo) {
        try {
            const { data } = await api.get(`${BASE}/por-codigo/${encodeURIComponent(codigo)}`);
            return data ?? null;
        } catch (err) {
            if (ehNaoEncontrado(err)) return null;
            throw err;
        }
    },

    async criar(cupom) {
        const { data } = await api.post(BASE, montarCorpo(cupom));
        return data;
    },

    async atualizar(id, cupom) {
        const { data } = await api.put(`${BASE}/${id}`, montarCorpo(cupom));
        return data;
    },

    async remover(id) {
        await api.delete(`${BASE}/${id}`);
    },

    /** GET /{id}/usos — quanto a campanha custou, nao quantas vezes foi digitada. */
    async listarUsos(id, { page, pageSize } = {}) {
        const { data } = await api.get(`${BASE}/${id}/usos`, { params: { page, pageSize } });
        return normalizarPagina(data, pageSize);
    },
};

export default cuponsService;
