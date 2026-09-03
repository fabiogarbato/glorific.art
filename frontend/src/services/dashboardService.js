import api from "@/api/client.js";

/**
 * DashboardAdminController — GET /api/v1/admin/dashboard (policy PainelAdmin).
 *
 * Sem `de`/`ate` o servico assume os ultimos trinta dias. Os blocos operacionais
 * (estoque critico, fila de envio, moderacao) NAO respeitam o periodo de
 * proposito: pendencia nao expira porque o filtro do painel mudou.
 */
export const dashboardService = {
    async obterResumo({ de, ate } = {}) {
        const { data } = await api.get("/admin/dashboard", { params: { de, ate } });

        return {
            periodoInicio: data?.periodoInicio ?? null,
            periodoFim: data?.periodoFim ?? null,

            faturamentoCentavos: data?.faturamentoCentavos ?? 0,
            pedidosPagos: data?.pedidosPagos ?? 0,
            ticketMedioCentavos: data?.ticketMedioCentavos ?? 0,
            freteCobradoCentavos: data?.freteCobradoCentavos ?? 0,
            descontoConcedidoCentavos: data?.descontoConcedidoCentavos ?? 0,

            pedidosPorStatus: data?.pedidosPorStatus ?? [],
            produtosMaisVendidos: data?.produtosMaisVendidos ?? [],

            totalEstoqueAbaixoDoMinimo: data?.totalEstoqueAbaixoDoMinimo ?? 0,
            estoqueCritico: data?.estoqueCritico ?? [],

            totalEnviosComProblema: data?.totalEnviosComProblema ?? 0,
            filaEnvioComProblema: data?.filaEnvioComProblema ?? [],

            avaliacoesPendentes: data?.avaliacoesPendentes ?? 0,
        };
    },
};

export default dashboardService;
