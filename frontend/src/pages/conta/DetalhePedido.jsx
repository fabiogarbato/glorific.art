import { Link, useParams } from "react-router-dom";
import { FiArrowLeft, FiExternalLink } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { LayoutConta } from "@/components/compra/NavConta.jsx";
import StatusPedidoBadge from "@/components/compra/StatusPedidoBadge.jsx";
import LinhaDoTempoPedido from "@/components/compra/LinhaDoTempoPedido.jsx";
import { usePedido, useRastreio } from "@/hooks/usePedidos.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { formatarData, formatarDataHora } from "@/utils/datas.js";
import { formatCEP, formatTelefone } from "@/utils/masks.js";

/**
 * Recibo do pedido.
 *
 * Tudo aqui é snapshot congelado na compra: nome da peça, foto, preço e endereço
 * são os do momento do pedido, não os do catálogo de hoje.
 *
 * A URL da etiqueta NUNCA é exibida — ela só existe para a expedição, no painel.
 */
function Bloco({ titulo, children, className = "" }) {
    return (
        <section aria-label={titulo} className={`flex flex-col gap-4 ${className}`}>
            <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>
            {children}
        </section>
    );
}

function Valor({ rotulo, valor, destaque = false }) {
    return (
        <div className="flex items-baseline justify-between gap-4">
            <dt className={destaque ? "font-sans text-sm text-ink" : "text-sm text-ink-soft"}>
                {rotulo}
            </dt>
            <dd className={`preco ${destaque ? "text-base text-ink" : "text-sm text-ink"}`}>
                {valor}
            </dd>
        </div>
    );
}

export default function DetalhePedido() {
    const { uuid } = useParams();
    const { pedido, isLoading, isError } = usePedido(uuid);

    // Só busca rastreio quando já existe pedido: sem ele o uuid pode nem ser válido.
    const { eventos } = useRastreio(uuid, { habilitado: !!pedido });

    if (isLoading) {
        return (
            <LayoutConta titulo="Pedido">
                <div className="flex flex-col gap-6" aria-busy="true">
                    <Skeleton className="h-8 w-56" />
                    <Skeleton className="h-40 w-full" />
                    <Skeleton className="h-40 w-full" />
                </div>
            </LayoutConta>
        );
    }

    if (isError || !pedido) {
        return (
            <LayoutConta titulo="Pedido não encontrado">
                <p className="max-w-lg text-base leading-relaxed text-ink-soft">
                    Este pedido não existe ou não pertence à sua conta. Se você acabou de comprar,
                    aguarde alguns instantes e recarregue a lista.
                </p>
                <Botao to="/conta/pedidos" variante="contorno" className="mt-8">
                    <FiArrowLeft size={14} aria-hidden="true" />
                    Voltar aos meus pedidos
                </Botao>
            </LayoutConta>
        );
    }

    const { pagamento, envio, enderecoEntrega } = pedido;
    const aguardandoPagamento = pedido.status === "AguardandoPagamento";

    return (
        <LayoutConta titulo={`Pedido ${pedido.numero}`}>
            <div className="flex flex-wrap items-center gap-4 pb-8">
                <StatusPedidoBadge status={pedido.status} />
                <p className="text-sm text-ink-soft">
                    Feito em {formatarDataHora(pedido.dataCriacao)}
                </p>
                <Link
                    to="/conta/pedidos"
                    className="ml-auto font-sans text-xs uppercase tracking-widest text-ink-soft underline decoration-sand underline-offset-4 hover:text-ink"
                >
                    Todos os pedidos
                </Link>
            </div>

            {/* -------------------------------- pagamento ainda em aberto */}
            {aguardandoPagamento && pagamento?.paymentUrl && (
                <div className="mb-10 flex flex-wrap items-center justify-between gap-4 border border-brass bg-linen px-5 py-4">
                    <p className="text-sm leading-relaxed text-ink">
                        Este pedido ainda aguarda o pagamento
                        {pagamento.expiraEm
                            ? `. O link vale até ${formatarDataHora(pagamento.expiraEm)}.`
                            : "."}
                    </p>
                    <Botao href={pagamento.paymentUrl} tamanho="sm">
                        Concluir pagamento
                        <FiExternalLink size={13} aria-hidden="true" />
                    </Botao>
                </div>
            )}

            {pedido.motivoCancelamento && (
                <p
                    role="status"
                    className="mb-10 border-l-2 border-danger bg-linen px-4 py-3 text-sm text-ink"
                >
                    Cancelado em {formatarData(pedido.dataCancelamento)}:{" "}
                    {pedido.motivoCancelamento}
                </p>
            )}

            <div className="grid gap-12 lg:grid-cols-[1fr_320px] lg:gap-14">
                <div className="flex flex-col gap-12">
                    {/* -------------------------------------------------- itens */}
                    <Bloco titulo="Peças">
                        <ul className="flex flex-col">
                            {pedido.itens.map((item, indice) => (
                                <li
                                    key={`${item.sku}-${indice}`}
                                    className="flex gap-4 border-b border-sand py-4 first:border-t"
                                >
                                    {item.imagemUrl ? (
                                        <img
                                            src={item.imagemUrl}
                                            alt=""
                                            loading="lazy"
                                            className="aspect-product w-16 shrink-0 object-cover"
                                        />
                                    ) : (
                                        <div
                                            aria-hidden="true"
                                            className="aspect-product w-16 shrink-0 border border-sand bg-linen"
                                        />
                                    )}

                                    <div className="min-w-0 flex-1">
                                        <p className="font-sans text-sm text-ink">
                                            {item.nomeProduto}
                                        </p>
                                        <p className="mt-0.5 text-sm text-ink-soft">
                                            {[
                                                item.tamanho && `Tamanho ${item.tamanho}`,
                                                item.cor,
                                            ]
                                                .filter(Boolean)
                                                .join(" · ") || item.sku}
                                        </p>
                                        <p className="preco mt-1 text-xs text-taupe">
                                            {item.quantidade} ×{" "}
                                            {formatarCentavosParaBRL(item.precoUnitarioCentavos)}
                                        </p>
                                    </div>

                                    <p className="preco shrink-0 text-sm text-ink">
                                        {formatarCentavosParaBRL(item.totalLinhaCentavos)}
                                    </p>
                                </li>
                            ))}
                        </ul>
                    </Bloco>

                    <LinhaDoTempoPedido historico={pedido.historico} eventos={eventos} />
                </div>

                {/* ------------------------------------------------ coluna lateral */}
                <aside className="flex flex-col gap-10">
                    <Bloco titulo="Valores">
                        <dl className="flex flex-col gap-3 border border-sand bg-linen px-5 py-5">
                            <Valor
                                rotulo="Subtotal"
                                valor={formatarCentavosParaBRL(pedido.subtotalCentavos)}
                            />
                            {pedido.descontoCupomCentavos > 0 && (
                                <Valor
                                    rotulo={
                                        pedido.codigoCupom
                                            ? `Desconto · ${pedido.codigoCupom}`
                                            : "Desconto"
                                    }
                                    valor={`− ${formatarCentavosParaBRL(pedido.descontoCupomCentavos)}`}
                                />
                            )}
                            <Valor
                                rotulo="Frete"
                                valor={
                                    pedido.freteCentavos === 0
                                        ? "Cortesia"
                                        : formatarCentavosParaBRL(pedido.freteCentavos)
                                }
                            />
                            <div className="filete my-1" />
                            <Valor
                                rotulo="Total"
                                destaque
                                valor={formatarCentavosParaBRL(pedido.totalCentavos)}
                            />
                        </dl>
                    </Bloco>

                    {enderecoEntrega && (
                        <Bloco titulo="Entrega">
                            <address className="text-sm not-italic leading-relaxed text-ink-soft">
                                {enderecoEntrega.destinatario}
                                <br />
                                {enderecoEntrega.logradouro}, {enderecoEntrega.numero}
                                {enderecoEntrega.complemento
                                    ? ` — ${enderecoEntrega.complemento}`
                                    : ""}
                                <br />
                                {enderecoEntrega.bairro} · {enderecoEntrega.cidade}/
                                {enderecoEntrega.uf}
                                <br />
                                {formatCEP(enderecoEntrega.cep)}
                                <br />
                                {formatTelefone(enderecoEntrega.telefoneContato)}
                            </address>

                            {(pedido.servicoFrete || pedido.prazoFreteDias) && (
                                <p className="text-sm text-ink-soft">
                                    {[pedido.transportadoraFrete, pedido.servicoFrete]
                                        .filter(Boolean)
                                        .join(" · ")}
                                    {pedido.prazoFreteDias
                                        ? ` · até ${pedido.prazoFreteDias} dias úteis`
                                        : ""}
                                </p>
                            )}

                            {envio?.codigoRastreio && (
                                <p className="border border-sand px-4 py-3 font-sans text-xs uppercase tracking-widest text-ink">
                                    Rastreio {envio.codigoRastreio}
                                </p>
                            )}
                        </Bloco>
                    )}

                    {pagamento && (
                        <Bloco titulo="Pagamento">
                            <dl className="flex flex-col gap-2 text-sm text-ink-soft">
                                <div className="flex justify-between gap-4">
                                    <dt>Situação</dt>
                                    <dd>
                                        <StatusPedidoBadge
                                            mapa="pagamento"
                                            status={pagamento.status}
                                        />
                                    </dd>
                                </div>
                                {pagamento.metodo && (
                                    <div className="flex justify-between gap-4">
                                        <dt>Meio</dt>
                                        <dd className="text-ink">{pagamento.metodo}</dd>
                                    </div>
                                )}
                                {pagamento.parcelas > 1 && (
                                    <div className="flex justify-between gap-4">
                                        <dt>Parcelas</dt>
                                        <dd className="preco text-ink">{pagamento.parcelas}×</dd>
                                    </div>
                                )}
                                {pagamento.dataConfirmacao && (
                                    <div className="flex justify-between gap-4">
                                        <dt>Confirmado em</dt>
                                        <dd className="text-ink">
                                            {formatarData(pagamento.dataConfirmacao)}
                                        </dd>
                                    </div>
                                )}
                            </dl>
                        </Bloco>
                    )}

                    {pedido.observacaoCliente && (
                        <Bloco titulo="Sua observação">
                            <p className="text-sm leading-relaxed text-ink-soft">
                                {pedido.observacaoCliente}
                            </p>
                        </Bloco>
                    )}
                </aside>
            </div>
        </LayoutConta>
    );
}
