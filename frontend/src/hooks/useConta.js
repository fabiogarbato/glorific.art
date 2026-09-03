import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { contaService } from "@/services/contaService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/** Perfil do cliente logado. O dono sai do token — nunca de parametro. */
export function usePerfil() {
    const query = useQuery({
        queryKey: queryKeys.conta.perfil(),
        queryFn: contaService.obterPerfil,
        staleTime: 1000 * 60,
    });

    return {
        perfil: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

export function useAtualizarPerfil() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: contaService.atualizarPerfil,
        onSuccess: (perfil) => {
            queryClient.setQueryData(queryKeys.conta.perfil(), perfil);
        },
    });
}

/**
 * Enderecos do cliente. Lista curta e sem paginacao no backend, entao a tela
 * carrega tudo de uma vez.
 */
export function useEnderecos() {
    const query = useQuery({
        queryKey: queryKeys.conta.enderecos(),
        queryFn: contaService.listarEnderecos,
        staleTime: 1000 * 60,
    });

    const enderecos = query.data ?? [];

    return {
        enderecos,
        /** Atalho usado pelo checkout para pre-selecionar a entrega. */
        principal: enderecos.find((e) => e.principal) ?? enderecos[0] ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/**
 * Todas as escritas de endereco invalidam a mesma lista. Promover a principal
 * mexe nos OUTROS enderecos (so pode existir um), entao atualizar so a linha
 * tocada deixaria dois principais na tela.
 */
function useMutacaoEndereco(mutationFn) {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.conta.enderecos() });
        },
    });
}

export function useCriarEndereco() {
    return useMutacaoEndereco(contaService.criarEndereco);
}

export function useAtualizarEndereco() {
    return useMutacaoEndereco(({ id, dados }) => contaService.atualizarEndereco(id, dados));
}

export function useRemoverEndereco() {
    return useMutacaoEndereco(contaService.removerEndereco);
}

export function useDefinirEnderecoPrincipal() {
    return useMutacaoEndereco(contaService.definirPrincipal);
}
