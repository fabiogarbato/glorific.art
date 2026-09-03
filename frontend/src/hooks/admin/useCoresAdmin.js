import { useQuery } from "@tanstack/react-query";
import { coresAdminService } from "@/services/admin/coresAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useCoresAdmin(filtros = {}) {
    return useListaCrudAdmin(coresAdminService, chavesCatalogo.cores, filtros);
}

export function useMutacoesCor() {
    return useCrudAdmin(coresAdminService, chavesCatalogo.cores);
}

/** Cores ativas ordenadas — base da matriz de variacoes e do vinculo foto/cor. */
export function useCoresAtivas() {
    const query = useQuery({
        queryKey: chavesCatalogo.cores.ativas(),
        queryFn: () => coresAdminService.ativas(),
        staleTime: 1000 * 60 * 5,
    });

    return {
        cores: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}
