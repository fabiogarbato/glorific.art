import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { usuariosAdminService } from "@/services/usuariosAdminService.js";
import { chavesOperacao } from "@/lib/queryKeysAdminOperacao.js";
import { PAGINA_VAZIA } from "@/lib/pagedResult.js";
import { useToast } from "@/hooks/useToast.js";

export function useUsuariosAdmin(filtros = {}) {
    const query = useQuery({
        queryKey: chavesOperacao.usuarios.lista(filtros),
        queryFn: () => usuariosAdminService.listar(filtros),
        placeholderData: (anterior) => anterior,
    });

    const pagina = query.data ?? PAGINA_VAZIA;

    return {
        usuarios: pagina.itens,
        total: pagina.total,
        pagina: pagina.pagina,
        totalPaginas: pagina.totalPaginas,
        tamanhoPagina: pagina.tamanhoPagina,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/**
 * Conceder e revogar papel derrubam sessao no servidor e mudam o que a pessoa
 * enxerga do painel. A lista inteira e invalidada porque a resposta traz so o
 * usuario alterado e a pagina pode estar filtrada justamente por papel.
 */
export function useAcoesUsuario() {
    const queryClient = useQueryClient();
    const toast = useToast();

    const invalidar = () =>
        queryClient.invalidateQueries({ queryKey: chavesOperacao.usuarios.all });

    const atualizar = useMutation({
        mutationFn: ({ id, ...dados }) => usuariosAdminService.atualizar(id, dados),
        onSuccess: () => {
            invalidar();
            toast.success("Cadastro atualizado.");
        },
    });

    const concederPapel = useMutation({
        mutationFn: ({ id, papel }) => usuariosAdminService.concederPapel(id, papel),
        onSuccess: () => {
            invalidar();
            toast.success("Papel concedido.");
        },
    });

    const revogarPapel = useMutation({
        mutationFn: ({ id, papel }) => usuariosAdminService.revogarPapel(id, papel),
        onSuccess: () => {
            invalidar();
            toast.success("Papel revogado. As sessões dessa pessoa foram encerradas.");
        },
    });

    const desativar = useMutation({
        mutationFn: (id) => usuariosAdminService.desativar(id),
        onSuccess: () => {
            invalidar();
            toast.success("Conta desativada.");
        },
    });

    const ativar = useMutation({
        mutationFn: (id) => usuariosAdminService.ativar(id),
        onSuccess: () => {
            invalidar();
            toast.success("Conta reativada.");
        },
    });

    return { atualizar, concederPapel, revogarPapel, desativar, ativar };
}

export default useUsuariosAdmin;
