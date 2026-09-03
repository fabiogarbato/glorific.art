import { useMutation, useQueryClient } from "@tanstack/react-query";
import { midiasAdminService } from "@/services/admin/midiasAdminService.js";
import { chavesCatalogo } from "@/lib/queryKeysAdminCatalogo.js";
import { useCrudAdmin, useListaCrudAdmin } from "./useCrudAdmin.js";

export function useMidiasAdmin(filtros = {}) {
    return useListaCrudAdmin(midiasAdminService, chavesCatalogo.midias, filtros);
}

export function useMutacoesMidia() {
    const queryClient = useQueryClient();
    const crud = useCrudAdmin(midiasAdminService, chavesCatalogo.midias);

    const invalidar = () =>
        queryClient.invalidateQueries({ queryKey: chavesCatalogo.midias.all });

    const enviar = useMutation({
        mutationFn: ({ arquivo, altText }) => midiasAdminService.enviar(arquivo, altText),
        onSuccess: invalidar,
    });

    const atualizarAltText = useMutation({
        mutationFn: ({ id, altText }) => midiasAdminService.atualizarAltText(id, altText),
        onSuccess: invalidar,
    });

    return { ...crud, enviar, atualizarAltText };
}
