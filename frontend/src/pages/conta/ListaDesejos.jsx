import { Link } from "react-router-dom";
import { FiShoppingBag, FiX } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Badge from "@/components/ui/Badge.jsx";
import { SkeletonCard } from "@/components/ui/Skeleton.jsx";
import { LayoutConta } from "@/components/compra/NavConta.jsx";
import { useListaDesejos, useRemoverListaDesejos } from "@/hooks/useListaDesejos.js";
import { useCarrinho } from "@/hooks/useCarrinho.js";
import { useToast } from "@/hooks/useToast.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Lista de desejos.
 *
 * Peça que saiu do catálogo (`produtoAtivo: false`) continua aparecendo, marcada
 * como indisponível: é justamente sobre ela que a pessoa quer ser avisada quando
 * voltar. Sumir com a linha calada seria o pior dos dois mundos.
 *
 * O botão de sacola só aparece quando a variação foi escolhida no momento de
 * favoritar — moda vende variação, e adivinhar tamanho pela pessoa daria troca.
 */
export default function ListaDesejos() {
    const { itens, isLoading, isError, refetch } = useListaDesejos();
    const remover = useRemoverListaDesejos();
    const { adicionar, salvando } = useCarrinho();
    const toast = useToast();

    async function levarParaSacola(item) {
        try {
            await adicionar({ idVariacao: item.idVariacao }, 1);
            toast.success("Peça adicionada à sacola.");
        } catch {
            /* o interceptor já mostrou o motivo */
        }
    }

    return (
        <LayoutConta
            titulo="Lista de desejos"
            descricao="As peças que você separou para pensar com calma."
        >
            {isLoading && (
                <div className="grid grid-cols-2 gap-x-4 gap-y-10 sm:grid-cols-3" aria-busy="true">
                    {[0, 1, 2].map((i) => (
                        <SkeletonCard key={i} />
                    ))}
                </div>
            )}

            {isError && (
                <div>
                    <p className="text-base text-ink">
                        Não conseguimos carregar sua lista agora.
                    </p>
                    <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                        Tentar de novo
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && itens.length === 0 && (
                <div className="border border-sand bg-linen px-6 py-12 text-center">
                    <h2 className="font-display text-xl tracking-tight text-ink">
                        Sua lista está vazia
                    </h2>
                    <p className="mx-auto mt-4 max-w-md text-base leading-relaxed text-ink-soft">
                        Toque no coração de qualquer peça da vitrine para guardá-la aqui. A lista é
                        só sua e não expira.
                    </p>
                    <Botao to="/catalogo" className="mt-8">
                        Ver a vitrine
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && itens.length > 0 && (
                <ul className="grid grid-cols-2 gap-x-4 gap-y-10 sm:grid-cols-3">
                    {itens.map((item) => {
                        const foraDeCatalogo = !item.produtoAtivo;
                        const variacaoEsgotada = item.variacaoDisponivel === false;
                        const indisponivel = foraDeCatalogo || variacaoEsgotada;

                        return (
                            <li key={item.id} className="group relative flex flex-col gap-3">
                                <Link
                                    to={`/produto/${item.slugProduto}`}
                                    className="block"
                                    tabIndex={foraDeCatalogo ? -1 : undefined}
                                >
                                    {item.imagemUrl ? (
                                        <img
                                            src={item.imagemUrl}
                                            alt={item.nomeProduto}
                                            loading="lazy"
                                            className={`aspect-product w-full object-cover ${indisponivel ? "opacity-50" : ""}`}
                                        />
                                    ) : (
                                        <div
                                            aria-hidden="true"
                                            className="aspect-product w-full border border-sand bg-linen"
                                        />
                                    )}
                                </Link>

                                <button
                                    type="button"
                                    onClick={() => remover.mutate(item.idProduto)}
                                    disabled={remover.isPending}
                                    aria-label={`Remover ${item.nomeProduto} da lista de desejos`}
                                    className="absolute right-2 top-2 flex h-9 w-9 items-center justify-center bg-base-100/90 text-ink-soft transition-colors hover:text-danger disabled:opacity-40"
                                >
                                    <FiX size={16} />
                                </button>

                                <div className="flex flex-col gap-1">
                                    <h2 className="font-display text-base leading-snug tracking-tight text-ink">
                                        <Link
                                            to={`/produto/${item.slugProduto}`}
                                            className="hover:text-olive"
                                        >
                                            {item.nomeProduto}
                                        </Link>
                                    </h2>

                                    {(item.tamanhoVariacao || item.corVariacao) && (
                                        <p className="text-sm text-ink-soft">
                                            {[
                                                item.tamanhoVariacao &&
                                                    `Tamanho ${item.tamanhoVariacao}`,
                                                item.corVariacao,
                                            ]
                                                .filter(Boolean)
                                                .join(" · ")}
                                        </p>
                                    )}

                                    <p className="preco mt-1 flex items-baseline gap-2 text-sm text-ink">
                                        {formatarCentavosParaBRL(item.precoCentavos)}
                                        {item.precoComparativoCentavos > item.precoCentavos && (
                                            <span className="text-xs text-taupe line-through">
                                                {formatarCentavosParaBRL(
                                                    item.precoComparativoCentavos,
                                                )}
                                            </span>
                                        )}
                                    </p>

                                    {indisponivel ? (
                                        <Badge variante="esgotado" className="mt-2 self-start">
                                            {foraDeCatalogo ? "Fora de linha" : "Esgotado"}
                                        </Badge>
                                    ) : (
                                        item.idVariacao && (
                                            <Botao
                                                variante="contorno"
                                                tamanho="sm"
                                                className="mt-3"
                                                disabled={salvando}
                                                onClick={() => levarParaSacola(item)}
                                            >
                                                <FiShoppingBag size={13} aria-hidden="true" />
                                                Levar para a sacola
                                            </Botao>
                                        )
                                    )}
                                </div>
                            </li>
                        );
                    })}
                </ul>
            )}
        </LayoutConta>
    );
}
