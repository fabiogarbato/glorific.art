import { useQuery } from "@tanstack/react-query";
import { tamanhosAdminService } from "@/services/admin/tamanhosAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useTamanhosAdmin(filtros = {}) {
    return useListaCrudAdmin(tamanhosAdminService, chavesCatalogo.tamanhos, filtros);
}

export function useMutacoesTamanho() {
    return useCrudAdmin(tamanhosAdminService, chavesCatalogo.tamanhos);
}

/** Tamanhos ativos ja na ordem de exibicao (PP, P, M, G, GG) — base da matriz de variacoes. */
export function useTamanhosAtivos(grade = null) {
    const query = useQuery({
        queryKey: chavesCatalogo.tamanhos.ativos(grade),
        queryFn: () => tamanhosAdminService.ativos(grade),
        staleTime: 1000 * 60 * 5,
    });

    return {
        tamanhos: query.data ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}
