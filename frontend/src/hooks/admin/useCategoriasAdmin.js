import { useQuery } from "@tanstack/react-query";
import {
    achatarArvore,
    categoriasAdminService,
} from "@/services/admin/categoriasAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useCategoriasAdmin(filtros = {}) {
    return useListaCrudAdmin(categoriasAdminService, chavesCatalogo.categorias, filtros);
}

export function useMutacoesCategoria() {
    return useCrudAdmin(categoriasAdminService, chavesCatalogo.categorias);
}

/**
 * Arvore completa — sem paginacao. Alimenta o seletor de categoria pai e o
 * filtro de categoria da lista de produtos.
 *
 * `somenteHabilitadas = false` no painel de proposito: o admin edita o que a
 * loja esconde.
 */
export function useArvoreCategorias(somenteHabilitadas = false) {
    const query = useQuery({
        queryKey: chavesCatalogo.categorias.arvore(somenteHabilitadas),
        queryFn: () => categoriasAdminService.arvore(somenteHabilitadas),
        staleTime: 1000 * 60 * 5,
    });

    const arvore = query.data ?? [];

    return {
        arvore,
        /** `[{ ...categoria, profundidade }]` pronto para um `<select>` indentado. */
        opcoes: achatarArvore(arvore),
        isLoading: query.isLoading,
        isError: query.isError,
    };
}
