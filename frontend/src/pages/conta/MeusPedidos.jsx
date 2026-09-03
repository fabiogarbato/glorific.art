import { useState } from "react";
import { Link } from "react-router-dom";
import { FiChevronRight } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import { LayoutConta } from "@/components/compra/NavConta.jsx";
import StatusPedidoBadge from "@/components/compra/StatusPedidoBadge.jsx";
import { useMeusPedidos } from "@/hooks/usePedidos.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { formatarData } from "@/utils/datas.js";

/**
 * Meus pedidos.
 *
 * A listagem é enxuta de propósito: o backend não carrega itens, endereço nem
 * histórico aqui. Tudo isso está no detalhe.
 *
 * `imagemUrl` é a miniatura do primeiro item JÁ CONGELADA no pedido — nunca lida
 * do catálogo, para que trocar a foto de um produto não reescreva recibo antigo.
 */
export default function MeusPedidos() {
    const [pagina, setPagina] = useState(1);
    const { pedidos, totalPaginas, total, itensPorPagina, isLoading, isError, refetch } =
        useMeusPedidos(pagina);

    return (
        <LayoutConta
            titulo="Meus pedidos"
            descricao="Tudo o que você já comprou, do mais recente para o mais antigo."
        >
            {isLoading && (
                <div className="flex flex-col gap-4" aria-busy="true">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-28 w-full" />
                    ))}
                </div>
            )}

            {isError && (
                <div>
                    <p className="text-base text-ink">
                        Não conseguimos carregar seus pedidos agora.
                    </p>
                    <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                        Tentar de novo
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && pedidos.length === 0 && (
                <div className="border border-sand bg-linen px-6 py-12 text-center">
                    <h2 className="font-display text-xl tracking-tight text-ink">
                        Você ainda não fez nenhum pedido
                    </h2>
                    <p className="mx-auto mt-4 max-w-md text-base leading-relaxed text-ink-soft">
                        Quando a primeira peça chegar até aqui, o pedido aparece nesta lista com o
                        rastreio completo.
                    </p>
                    <Botao to="/catalogo" className="mt-8">
                        Ver a vitrine
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && pedidos.length > 0 && (
                <>
                    <ul className="flex flex-col gap-3">
                        {pedidos.map((pedido) => (
                            <li key={pedido.uuid}>
                                <Link
                                    to={`/conta/pedidos/${pedido.uuid}`}
                                    className="flex items-center gap-4 border border-sand px-4 py-4 transition-colors hover:border-taupe sm:gap-5 sm:px-5"
                                >
                                    {pedido.imagemUrl ? (
                                        <img
                                            src={pedido.imagemUrl}
                                            alt=""
                                            loading="lazy"
                                            className="aspect-product w-16 shrink-0 object-cover sm:w-20"
                                        />
                                    ) : (
                                        <div
                                            aria-hidden="true"
                                            className="aspect-product w-16 shrink-0 border border-sand bg-linen sm:w-20"
                                        />
                                    )}

                                    <div className="min-w-0 flex-1">
                                        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                                            <span className="font-sans text-sm text-ink">
                                                Pedido {pedido.numero}
                                            </span>
                                            <StatusPedidoBadge status={pedido.status} />
                                        </div>

                                        <p className="mt-1.5 text-sm text-ink-soft">
                                            {formatarData(pedido.dataCriacao)} ·{" "}
                                            {pedido.quantidadeItens}{" "}
                                            {pedido.quantidadeItens === 1 ? "peça" : "peças"}
                                        </p>

                                        {pedido.codigoRastreio && (
                                            <p className="mt-1 font-sans text-xs uppercase tracking-widest text-taupe">
                                                Rastreio {pedido.codigoRastreio}
                                            </p>
                                        )}
                                    </div>

                                    <div className="flex shrink-0 items-center gap-3">
                                        <span className="preco text-sm text-ink">
                                            {formatarCentavosParaBRL(pedido.totalCentavos)}
                                        </span>
                                        <FiChevronRight
                                            size={16}
                                            className="text-taupe"
                                            aria-hidden="true"
                                        />
                                    </div>
                                </Link>
                            </li>
                        ))}
                    </ul>

                    <Paginacao
                        className="mt-10"
                        paginaAtual={pagina}
                        totalPaginas={totalPaginas}
                        totalItens={total}
                        itensPorPagina={itensPorPagina}
                        onMudarPagina={setPagina}
                    />
                </>
            )}
        </LayoutConta>
    );
}
