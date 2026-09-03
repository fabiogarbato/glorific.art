import { createContext, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { carrinhoService, CARRINHO_VAZIO } from "@/services/carrinhoService.js";
import { queryKeys } from "@/lib/queryKeys.js";
import { useAuth } from "@/hooks/useAuth.js";

export const CarrinhoContext = createContext(null);

/**
 * Carrinho SERVER-SIDE.
 *
 * Nao existe mais copia em localStorage: o dono do carrinho e o backend, que
 * identifica a pessoa pela claim `sub` do token ou, na falta dela, pelo cookie
 * httpOnly `gl_cart` que ele mesmo emite. Guardar itens no navegador voltaria a
 * mostrar preco velho e permitiria fechar pedido com peca esgotada.
 *
 * Todo endpoint de carrinho devolve o carrinho INTEIRO, entao cada mutation
 * apenas escreve a resposta no cache — sem refetch extra e sem estado paralelo.
 *
 * No login o carrinho anonimo e fundido no do usuario (POST /carrinho/merge).
 */

/**
 * Descobre o id da LINHA do carrinho a partir do que a tela passou.
 *
 * Aceita três formas, nesta ordem, e a ordem importa:
 *   1. o próprio item (`{ id }`) — é o que as telas desta área usam;
 *   2. um `idVariacao` — contrato antigo, ainda usado por quem só conhece a peça;
 *   3. um id de linha cru.
 *
 * Passar o item inteiro é o caminho SEM ambiguidade: id de linha e id de
 * variação são duas sequências independentes, e um número que é linha numa e
 * variação na outra faria a tela alterar a peça errada.
 */
function resolverIdItem(itens, chave) {
    if (chave && typeof chave === "object") return chave.id ?? null;

    const alvo = Number(chave);
    const porVariacao = itens.find((i) => Number(i.idVariacao) === alvo);
    if (porVariacao) return porVariacao.id;

    const porLinha = itens.find((i) => Number(i.id) === alvo);
    return porLinha ? porLinha.id : null;
}

export function CarrinhoProvider({ children }) {
    const queryClient = useQueryClient();
    const navigate = useNavigate();
    const { estaAutenticado, inicializando } = useAuth();
    const [aberto, setAberto] = useState(false);

    const query = useQuery({
        queryKey: queryKeys.carrinho.atual(),
        queryFn: carrinhoService.obter,
        // Preco e estoque envelhecem rapido; e barato reconferir ao voltar a aba.
        staleTime: 1000 * 15,
        refetchOnWindowFocus: true,
        retry: 1,
    });

    const carrinho = query.data ?? CARRINHO_VAZIO;
    const itens = carrinho.itens;

    /** Toda rota de carrinho responde com o carrinho completo — basta gravar. */
    const gravar = useCallback(
        (data) => queryClient.setQueryData(queryKeys.carrinho.atual(), data),
        [queryClient],
    );

    const mAdicionar = useMutation({
        mutationFn: carrinhoService.adicionarItem,
        onSuccess: gravar,
    });

    const mAlterar = useMutation({
        mutationFn: ({ idItem, quantidade }) =>
            carrinhoService.alterarQuantidade(idItem, quantidade),
        onSuccess: gravar,
    });

    const mRemover = useMutation({
        mutationFn: carrinhoService.removerItem,
        onSuccess: gravar,
    });

    const mEsvaziar = useMutation({
        mutationFn: carrinhoService.esvaziar,
        onSuccess: gravar,
    });

    const mAplicarCupom = useMutation({
        mutationFn: carrinhoService.aplicarCupom,
        onSuccess: gravar,
    });

    const mRemoverCupom = useMutation({
        mutationFn: carrinhoService.removerCupom,
        onSuccess: gravar,
    });

    // ------------------------------------------------------------------
    // Merge no login
    // ------------------------------------------------------------------
    // Dispara SO na transicao real "deslogado -> logado".
    //
    // O `inicializando` do AuthProvider e o que separa "acabei de entrar" de
    // "dei F5 numa sessao que ja existia": o access token vive em memoria, entao
    // todo reload comeca deslogado e so vira logado depois do refresh silencioso.
    // Sem essa guarda, cada recarregamento de pagina dispararia um /carrinho/merge.
    //
    // `mesclando` evita a chamada dupla do StrictMode em desenvolvimento.
    const eraAutenticado = useRef(estaAutenticado);
    const mesclando = useRef(false);

    useEffect(() => {
        if (inicializando) {
            // Enquanto a sessao nao se resolve, so acompanha o valor corrente.
            eraAutenticado.current = estaAutenticado;
            return;
        }

        if (estaAutenticado && !eraAutenticado.current && !mesclando.current) {
            mesclando.current = true;

            carrinhoService
                .mesclar()
                .then(gravar)
                .catch(() => {
                    // O merge falhou (rede, 401 numa corrida de token). O carrinho
                    // do servidor continua sendo a verdade: recarrega e segue.
                    queryClient.invalidateQueries({ queryKey: queryKeys.carrinho.all });
                })
                .finally(() => {
                    mesclando.current = false;
                });
        }

        // No logout o carrinho do cliente logado nao pode continuar na tela.
        if (!estaAutenticado && eraAutenticado.current) {
            queryClient.invalidateQueries({ queryKey: queryKeys.carrinho.all });
        }

        eraAutenticado.current = estaAutenticado;
    }, [estaAutenticado, inicializando, gravar, queryClient]);

    // ------------------------------------------------------------------
    // API do contexto
    // ------------------------------------------------------------------

    /** `adicionar(produtoOuVariacao, quantidade)` — aceita o objeto do card ou o id cru. */
    const adicionar = useCallback(
        (item, quantidade = 1) => {
            const idVariacao = typeof item === "object" && item !== null ? item.idVariacao : item;
            return mAdicionar.mutateAsync({ idVariacao, quantidade });
        },
        [mAdicionar],
    );

    const alterarQuantidade = useCallback(
        (chave, quantidade) => {
            const idItem = resolverIdItem(itens, chave);
            if (idItem == null) return Promise.resolve(carrinho);
            return mAlterar.mutateAsync({ idItem, quantidade });
        },
        [itens, carrinho, mAlterar],
    );

    const remover = useCallback(
        (chave) => {
            const idItem = resolverIdItem(itens, chave);
            if (idItem == null) return Promise.resolve(carrinho);
            return mRemover.mutateAsync(idItem);
        },
        [itens, carrinho, mRemover],
    );

    const limpar = useCallback(() => mEsvaziar.mutateAsync(), [mEsvaziar]);

    const aplicarCupom = useCallback(
        (codigo) => mAplicarCupom.mutateAsync(codigo),
        [mAplicarCupom],
    );

    const removerCupom = useCallback(() => mRemoverCupom.mutateAsync(), [mRemoverCupom]);

    const valor = useMemo(
        () => ({
            carrinho,
            itens,
            /** Soma das quantidades — e o numero do selo no cabecalho. */
            totalItens: carrinho.quantidadeItens,
            subtotalCentavos: carrinho.subtotalCentavos,
            descontoCentavos: carrinho.descontoCentavos,
            totalCentavos: carrinho.totalCentavos,
            codigoCupom: carrinho.codigoCupom,
            freteGratisPorCupom: carrinho.freteGratisPorCupom,
            avisoCupom: carrinho.avisoCupom,
            possuiItemIndisponivel: carrinho.possuiItemIndisponivel,
            possuiPrecoAlterado: carrinho.possuiPrecoAlterado,
            vazio: itens.length === 0,

            isLoading: query.isLoading,
            isError: query.isError,
            recarregar: query.refetch,
            /** Alguma escrita em voo — as telas usam para travar botao. */
            salvando:
                mAdicionar.isPending ||
                mAlterar.isPending ||
                mRemover.isPending ||
                mEsvaziar.isPending ||
                mAplicarCupom.isPending ||
                mRemoverCupom.isPending,

            adicionar,
            alterarQuantidade,
            remover,
            limpar,
            aplicarCupom,
            removerCupom,

            /**
             * O botão da sacola no cabeçalho chama `abrir()`. Como a loja não tem
             * gaveta lateral, ele leva para a página do carrinho — antes disso a
             * chamada só ligava um booleano que ninguém desenhava, e clicar na
             * sacola não fazia nada.
             *
             * `aberto`/`fechar` continuam existindo para quando a gaveta existir.
             */
            aberto,
            abrir: () => navigate("/carrinho"),
            fechar: () => setAberto(false),
        }),
        [
            navigate,
            carrinho,
            itens,
            query.isLoading,
            query.isError,
            query.refetch,
            mAdicionar.isPending,
            mAlterar.isPending,
            mRemover.isPending,
            mEsvaziar.isPending,
            mAplicarCupom.isPending,
            mRemoverCupom.isPending,
            adicionar,
            alterarQuantidade,
            remover,
            limpar,
            aplicarCupom,
            removerCupom,
            aberto,
        ],
    );

    return <CarrinhoContext.Provider value={valor}>{children}</CarrinhoContext.Provider>;
}

export default CarrinhoProvider;
