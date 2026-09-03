import { useQuery } from "@tanstack/react-query";
import { tabelasMedidasAdminService } from "@/services/admin/tabelasMedidasAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useTabelasMedidasAdmin(filtros = {}) {
    return useListaCrudAdmin(
        tabelasMedidasAdminService,
        chavesCatalogo.tabelasMedidas,
        filtros,
    );
}

export function useMutacoesTabelaMedidas() {
    return useCrudAdmin(tabelasMedidasAdminService, chavesCatalogo.tabelasMedidas);
}

/**
 * Detalhe com as linhas. A listagem paginada ja devolve `linhas`, mas o
 * formulario carrega o detalhe para nao editar em cima de uma copia velha.
 */
export function useTabelaMedidas(id) {
    const query = useQuery({
        queryKey: chavesCatalogo.tabelasMedidas.detalhe(id),
        queryFn: () => tabelasMedidasAdminService.obter(id),
        enabled: !!id,
        retry: false,
    });

    return {
        tabela: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
    };
}

/** Lista curta para o seletor de tabela de medidas no formulario de produto. */
export function useTabelasParaSelecao() {
    const lista = useTabelasMedidasAdmin({ pagina: 1, tamanhoPagina: 100 });
    return {
        tabelas: lista.itens,
        isLoading: lista.isLoading,
        isError: lista.isError,
    };
}
