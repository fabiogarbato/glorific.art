import { useQuery } from "@tanstack/react-query";
import { medidasService } from "@/services/medidasService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/**
 * Camada obrigatoria entre page e service: a tela recebe `tabelas` e `tabela`,
 * nunca `data`.
 *
 * Tabela de medidas muda quando o admin mexe na grade — coisa de meses, nao de
 * minutos. Dai o `staleTime` de meia hora: quem abre o guia, volta ao produto e
 * abre o guia de novo nao dispara uma segunda requisicao.
 */
const MEIA_HORA = 1000 * 60 * 30;

/**
 * Todas as tabelas ativas da loja.
 *
 * `tabelas.length === 0` sem erro e estado NORMAL (a loja ainda nao cadastrou
 * nenhuma), e a tela precisa distinguir isso de falha de rede — por isso
 * `isError` sai separado em vez de virar lista vazia.
 */
export function useTabelasMedidas() {
    const query = useQuery({
        queryKey: queryKeys.tabelasMedidas.lista(),
        queryFn: medidasService.listar,
        staleTime: MEIA_HORA,
    });

    return {
        tabelas: query.data ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        refetch: query.refetch,
    };
}

/** Uma tabela pelo id. `naoEncontrada` cobre o 404 (inativa ou inexistente). */
export function useTabelaMedidas(id) {
    const query = useQuery({
        queryKey: queryKeys.tabelasMedidas.detalhe(id),
        queryFn: () => medidasService.obter(id),
        enabled: id !== null && id !== undefined && id !== "",
        retry: false, // 404 aqui e "nao existe", nao falha transitoria
        staleTime: MEIA_HORA,
    });

    return {
        tabela: query.data ?? null,
        naoEncontrada: !query.isLoading && !query.isError && query.data === null,
        isLoading: query.isLoading,
        isError: query.isError,
        refetch: query.refetch,
    };
}

export default useTabelasMedidas;
