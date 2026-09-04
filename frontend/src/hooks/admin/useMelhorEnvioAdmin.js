import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { melhorEnvioAdminService } from "@/services/admin/melhorEnvioAdminService.js";

const CHAVE_STATUS = ["admin", "melhor-envio", "status"];

export function useStatusMelhorEnvio() {
    const query = useQuery({
        queryKey: CHAVE_STATUS,
        queryFn: melhorEnvioAdminService.status,
        staleTime: 1000 * 30,
    });

    return {
        status: query.data ?? null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

export function useConectarMelhorEnvio() {
    const queryClient = useQueryClient();

    const iniciar = useMutation({
        mutationFn: melhorEnvioAdminService.autorizar,
    });

    const completar = useMutation({
        mutationFn: ({ code, state }) => melhorEnvioAdminService.conectar(code, state),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_STATUS }),
    });

    return { iniciar, completar };
}
