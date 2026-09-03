import { useQuery } from "@tanstack/react-query";
import { dashboardService } from "@/services/dashboardService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";

/**
 * Resumo do painel. `periodo` e `{ de, ate }` ja no formato de parametro
 * (string UTC sem sufixo, vinda de `lib/periodo.js`) — a chave de cache usa o
 * mesmo objeto, entao trocar o preset refaz a consulta sem gambiarra de
 * `refetch` manual.
 */
export function useDashboard({ de, ate } = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.dashboard.resumo({ de, ate }),
        queryFn: () => dashboardService.obterResumo({ de, ate }),
        // O painel fica aberto o dia inteiro numa aba; meio minuto de frescor
        // evita uma rajada de consultas pesadas a cada troca de janela.
        staleTime: 30_000,
    });

    return {
        resumo: query.data ?? null,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        erro: query.error,
        refetch: query.refetch,
    };
}

export default useDashboard;
