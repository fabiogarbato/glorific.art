import { useMutation, useQueryClient } from "@tanstack/react-query";
import { colecoesAdminService } from "@/services/admin/colecoesAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useColecoesAdmin(filtros = {}) {
    return useListaCrudAdmin(colecoesAdminService, chavesCatalogo.colecoes, filtros);
}

export function useMutacoesColecao() {
    const queryClient = useQueryClient();
    const crud = useCrudAdmin(colecoesAdminService, chavesCatalogo.colecoes);

    // O vinculo muda a vitrine do drop E o detalhe do produto: invalida os dois escopos.
    const invalidarTudo = () => {
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.colecoes.all });
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.produtos.all });
    };

    const vincularProduto = useMutation({
        mutationFn: ({ idColecao, idProduto, ordem = 0 }) =>
            colecoesAdminService.vincularProduto(idColecao, { idProduto, ordem }),
        onSuccess: invalidarTudo,
    });

    const desvincularProduto = useMutation({
        mutationFn: ({ idColecao, idProduto }) =>
            colecoesAdminService.desvincularProduto(idColecao, idProduto),
        onSuccess: invalidarTudo,
    });

    return { ...crud, vincularProduto, desvincularProduto };
}

/**
 * Lista enxuta para o seletor de colecoes do formulario de produto.
 * Usa o teto de pagina do backend (100) porque nao existe rota "todas".
 */
export function useColecoesParaSelecao() {
    const lista = useListaCrudAdmin(colecoesAdminService, chavesCatalogo.colecoes, {
        pagina: 1,
        tamanhoPagina: 100,
    });

    return {
        colecoes: lista.itens,
        total: lista.total,
        isLoading: lista.isLoading,
        isError: lista.isError,
    };
}
