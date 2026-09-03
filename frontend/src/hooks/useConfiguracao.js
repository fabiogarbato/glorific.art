import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { configuracaoService } from "@/services/configuracaoService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { useToast } from "@/hooks/useToast.js";

/** Linha unica da configuracao da loja. */
export function useConfiguracao() {
    const query = useQuery({
        queryKey: chavesOperacao.configuracoes.atual(),
        queryFn: configuracaoService.obter,
        retry: false,
    });

    return {
        configuracao: query.data ?? null,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

export function useSalvarConfiguracao() {
    const queryClient = useQueryClient();
    const toast = useToast();

    return useMutation({
        mutationFn: (config) => configuracaoService.atualizar(config),
        onSuccess: (salvo) => {
            // A API devolve a linha inteira: escrever direto evita um GET a mais
            // e deixa o formulario com o valor exato que o servidor normalizou.
            if (salvo) queryClient.setQueryData(chavesOperacao.configuracoes.atual(), salvo);
            queryClient.invalidateQueries({ queryKey: chavesOperacao.configuracoes.all });
            toast.success("Configuração salva. Vale na próxima cotação de frete.");
        },
    });
}

export default useConfiguracao;
