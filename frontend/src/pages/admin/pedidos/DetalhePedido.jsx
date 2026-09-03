import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
    FiAlertTriangle,
    FiArrowLeft,
    FiExternalLink,
    FiPrinter,
    FiRefreshCw,
    FiTag,
    FiXCircle,
} from "react-icons/fi";

import BadgeStatus from "@/components/admin/BadgeStatus.jsx";
import { EstadoErro, EstadoVazio } from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

import { useAcoesPedido, usePedidoAdmin } from "@/hooks/usePedidosAdmin.js";
import { useDashboard } from "@/hooks/useDashboard.js";
import {
    STATUS_ENVIO,
    STATUS_PAGAMENTO,
    STATUS_PEDIDO,
    STATUS_PEDIDO_ENCERRADO,
    rotularStatusPedido,
    statusPedidoSelecionaveis,
} from "@/lib/statusAdmin.js";
import { formatarDataHora, formatarRelativo } from "@/utils/datas.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { formatCEP, formatTelefone } from "@/utils/masks.js";

function Linha({ rotulo, children }) {
    return (
        <div className="flex items-start justify-between gap-4 border-b border-sand/60 py-2 last:border-0">
            <dt className="text-xs uppercase tracking-widest text-ink-soft">{rotulo}</dt>
            <dd className="min-w-0 text-right text-sm text-ink">{children ?? "—"}</dd>
        </div>
    );
}

function Cartao({ titulo, acoes, children }) {
    return (
        <section className="border border-sand bg-base-100">
            <header className="flex flex-wrap items-center justify-between gap-2 border-b border-sand bg-linen px-4 py-3">
                <h2 className="font-display text-lg tracking-tight text-ink">{titulo}</h2>
                {acoes}
            </header>
            <div className="px-4 py-3">{children}</div>
        </section>
    );
}

/**
 * Detalhe operacional do pedido (policy Expedicao).
 *
 * Tudo que aparece nos itens e no endereço é SNAPSHOT gravado na compra:
 * renomear a peça ou trocar a foto no catálogo não reescreve recibo antigo.
 *
 * Duas assimetrias do backend viram decisão de tela aqui:
 *  1. cancelar NÃO é uma opção do seletor de status — o serviço recusa e manda
 *     usar a rota própria, que devolve estoque e cancela a etiqueta;
 *  2. o detalhe do pedido não carrega o último erro do envio. Esse texto só
 *     existe na fila do painel, então é de lá que ele é lido e casado pelo
 *     número do pedido.
 */
export default function DetalhePedido() {
    const { uuid } = useParams();

    const { pedido, isLoading, isFetching, isError } = usePedidoAdmin(uuid);
    const {
        alterarStatus,
        cancelar,
        gerarEtiqueta,
        sincronizarRastreio,
        gerarLinkPublicoEtiqueta,
    } = useAcoesPedido(uuid);

    // A fila de envio com problema ignora período no backend, então este resumo
    // serve só para descobrir o último erro deste pedido.
    const { resumo } = useDashboard();

    const [modalStatus, setModalStatus] = useState(false);
    const [novoStatus, setNovoStatus] = useState("");
    const [observacao, setObservacao] = useState("");

    const [modalCancelar, setModalCancelar] = useState(false);
    const [motivo, setMotivo] = useState("");
    const [erroMotivo, setErroMotivo] = useState("");

    const [linkPublico, setLinkPublico] = useState(null);

    const problemaEnvio = useMemo(
        () =>
            (resumo?.filaEnvioComProblema ?? []).find(
                (linha) => linha.numeroPedido === pedido?.numero,
            ) ?? null,
        [resumo, pedido?.numero],
    );

    const encerrado = pedido ? STATUS_PEDIDO_ENCERRADO.includes(pedido.status) : false;
    const opcoesStatus = pedido ? statusPedidoSelecionaveis(pedido.status) : [];

    if (isLoading) {
        return (
            <div className="animate-fade-up">
                <Skeleton className="h-8 w-64" />
                <div className="mt-8 grid gap-6 lg:grid-cols-3">
                    <Skeleton className="h-80 w-full lg:col-span-2" />
                    <Skeleton className="h-80 w-full" />
                </div>
            </div>
        );
    }

    if (isError) {
        return (
            <div className="animate-fade-up">
                <EstadoErro mensagem="Não foi possível carregar este pedido." />
            </div>
        );
    }

    if (!pedido) {
        return (
            <div className="animate-fade-up">
                <EstadoVazio
                    titulo="Pedido não encontrado"
                    mensagem="O identificador pode estar errado ou o pedido não existe mais nesta loja."
                    acao={
                        <Botao variante="contorno" tamanho="sm" to="/admin/pedidos">
                            Voltar para a fila
                        </Botao>
                    }
                />
            </div>
        );
    }

    const confirmarStatus = () => {
        if (!novoStatus) return;
        alterarStatus.mutate(
            { statusNovo: novoStatus, observacao },
            {
                onSuccess: () => {
                    setModalStatus(false);
                    setNovoStatus("");
                    setObservacao("");
                },
            },
        );
    };

    const confirmarCancelamento = () => {
        const texto = motivo.trim();
        if (texto.length < 3) {
            setErroMotivo("Descreva o motivo com pelo menos 3 caracteres.");
            return;
        }
        setErroMotivo("");
        cancelar.mutate(
            { motivo: texto },
            {
                onSuccess: () => {
                    setModalCancelar(false);
                    setMotivo("");
                },
            },
        );
    };

    const gerarLinkPublico = () =>
        gerarLinkPublicoEtiqueta.mutate(undefined, {
            // O aviso de "ainda não gerada" já sai do hook; aqui só abrimos o
            // painel quando existe endereço para mostrar.
            onSuccess: (url) => url && setLinkPublico(url),
        });

    return (
        <div className="animate-fade-up">
            <Link
                to="/admin/pedidos"
                className="inline-flex items-center gap-2 font-sans text-xs uppercase tracking-widest text-ink-soft hover:text-ink"
            >
                <FiArrowLeft size={14} aria-hidden="true" /> Voltar para a fila
            </Link>

            <header className="mt-4 flex flex-wrap items-end justify-between gap-4">
                <div className="min-w-0">
                    <p className="eyebrow">Pedido</p>
                    <h1 className="preco mt-2 font-display text-2xl tracking-tight text-ink">
                        {pedido.numero}
                    </h1>
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                        <BadgeStatus mapa={STATUS_PEDIDO} valor={pedido.status} />
                        <span className="text-xs text-ink-soft">
                            Criado em {formatarDataHora(pedido.dataCriacao)}
                        </span>
                    </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    <Botao
                        variante="contorno"
                        tamanho="sm"
                        disabled={encerrado || opcoesStatus.length === 0}
                        onClick={() => setModalStatus(true)}
                    >
                        Mudar status
                    </Botao>

                    <Botao
                        variante="sutil"
                        tamanho="sm"
                        onClick={() => gerarEtiqueta.mutate()}
                        carregando={gerarEtiqueta.isPending}
                        disabled={encerrado}
                    >
                        <FiTag size={14} aria-hidden="true" /> Gerar etiqueta
                    </Botao>

                    {pedido.envio?.urlEtiqueta ? (
                        <Botao
                            variante="sutil"
                            tamanho="sm"
                            href={pedido.envio.urlEtiqueta}
                            target="_blank"
                            rel="noreferrer"
                        >
                            <FiPrinter size={14} aria-hidden="true" /> Imprimir etiqueta
                        </Botao>
                    ) : null}

                    <Botao
                        variante="sutil"
                        tamanho="sm"
                        onClick={() => sincronizarRastreio.mutate()}
                        carregando={sincronizarRastreio.isPending}
                    >
                        <FiRefreshCw size={14} aria-hidden="true" /> Sincronizar rastreio
                    </Botao>

                    <Botao
                        variante="perigo"
                        tamanho="sm"
                        disabled={encerrado}
                        onClick={() => setModalCancelar(true)}
                    >
                        <FiXCircle size={14} aria-hidden="true" /> Cancelar
                    </Botao>
                </div>
            </header>

            {encerrado && (
                <p className="mt-4 border border-sand bg-linen px-4 py-3 text-sm text-ink-soft">
                    Este pedido está encerrado e não aceita mais mudança de status.
                    {pedido.motivoCancelamento
                        ? ` Motivo registrado: ${pedido.motivoCancelamento}`
                        : ""}
                </p>
            )}

            {isFetching && (
                <p className="mt-4 text-xs text-taupe" role="status">
                    Atualizando…
                </p>
            )}

            <div className="mt-8 grid gap-6 lg:grid-cols-3">
                {/* ------------------------------------------------- coluna larga */}
                <div className="flex flex-col gap-6 lg:col-span-2">
                    <Cartao titulo="Itens">
                        <ul className="divide-y divide-sand/60">
                            {pedido.itens.map((item, i) => (
                                <li key={`${item.sku}-${i}`} className="flex gap-4 py-3">
                                    <div className="h-20 w-16 shrink-0 overflow-hidden bg-linen">
                                        {item.imagemUrl ? (
                                            <img
                                                src={item.imagemUrl}
                                                alt={item.nomeProduto}
                                                className="h-full w-full object-cover"
                                                loading="lazy"
                                            />
                                        ) : (
                                            <div
                                                className="h-full w-full bg-sand/60"
                                                aria-hidden="true"
                                            />
                                        )}
                                    </div>

                                    <div className="min-w-0 flex-1">
                                        <p className="text-sm text-ink">{item.nomeProduto}</p>
                                        <p className="preco text-xs text-ink-soft">
                                            {item.sku} · {item.tamanho} · {item.cor}
                                        </p>
                                        <p className="preco mt-1 text-xs text-ink-soft">
                                            {item.quantidade} ×{" "}
                                            {formatarCentavosParaBRL(item.precoUnitarioCentavos)}
                                            {item.descontoUnitarioCentavos > 0
                                                ? ` − ${formatarCentavosParaBRL(item.descontoUnitarioCentavos)} de desconto por peça`
                                                : ""}
                                        </p>
                                    </div>

                                    <p className="preco shrink-0 text-sm text-ink">
                                        {formatarCentavosParaBRL(item.totalLinhaCentavos)}
                                    </p>
                                </li>
                            ))}
                        </ul>

                        <dl className="mt-4 border-t border-sand pt-3">
                            <Linha rotulo="Subtotal">
                                <span className="preco">
                                    {formatarCentavosParaBRL(pedido.subtotalCentavos)}
                                </span>
                            </Linha>
                            <Linha rotulo="Desconto do cupom">
                                <span className="preco">
                                    {pedido.descontoCupomCentavos > 0 ? "− " : ""}
                                    {formatarCentavosParaBRL(pedido.descontoCupomCentavos)}
                                    {pedido.codigoCupom ? ` (${pedido.codigoCupom})` : ""}
                                </span>
                            </Linha>
                            <Linha rotulo="Frete">
                                <span className="preco">
                                    {formatarCentavosParaBRL(pedido.freteCentavos)}
                                    {pedido.transportadoraFrete
                                        ? ` · ${pedido.transportadoraFrete}`
                                        : ""}
                                    {pedido.servicoFrete ? ` ${pedido.servicoFrete}` : ""}
                                </span>
                            </Linha>
                            <Linha rotulo="Total">
                                <span className="preco font-display text-lg text-ink">
                                    {formatarCentavosParaBRL(pedido.totalCentavos)}
                                </span>
                            </Linha>
                        </dl>
                    </Cartao>

                    <Cartao titulo="Histórico de status">
                        {pedido.historico.length === 0 ? (
                            <p className="py-6 text-center text-sm text-ink-soft">
                                Nenhuma mudança registrada além da criação do pedido.
                            </p>
                        ) : (
                            <ol className="relative border-l border-sand pl-5">
                                {pedido.historico.map((evento, i) => (
                                    <li key={`${evento.dataAlteracao}-${i}`} className="pb-5 last:pb-0">
                                        <span
                                            className="absolute -left-[5px] mt-1.5 h-2.5 w-2.5 bg-olive"
                                            aria-hidden="true"
                                        />
                                        <p className="text-sm text-ink">
                                            {evento.statusAnterior
                                                ? `${rotularStatusPedido(evento.statusAnterior)} → ${rotularStatusPedido(evento.statusNovo)}`
                                                : rotularStatusPedido(evento.statusNovo)}
                                        </p>
                                        <p className="text-xs text-ink-soft">
                                            {formatarDataHora(evento.dataAlteracao)} ·{" "}
                                            {formatarRelativo(evento.dataAlteracao)}
                                        </p>
                                        {evento.observacao && (
                                            <p className="mt-1 text-xs text-ink-soft">
                                                {evento.observacao}
                                            </p>
                                        )}
                                    </li>
                                ))}
                            </ol>
                        )}
                    </Cartao>
                </div>

                {/* --------------------------------------------------- coluna fina */}
                <div className="flex flex-col gap-6">
                    <Cartao titulo="Entrega">
                        {pedido.enderecoEntrega ? (
                            <address className="not-italic text-sm leading-relaxed text-ink">
                                <p className="font-medium">{pedido.enderecoEntrega.destinatario}</p>
                                <p className="preco text-ink-soft">
                                    {formatTelefone(pedido.enderecoEntrega.telefoneContato)}
                                </p>
                                <p className="mt-2">
                                    {pedido.enderecoEntrega.logradouro},{" "}
                                    {pedido.enderecoEntrega.numero}
                                    {pedido.enderecoEntrega.complemento
                                        ? ` — ${pedido.enderecoEntrega.complemento}`
                                        : ""}
                                </p>
                                <p>
                                    {pedido.enderecoEntrega.bairro} · {pedido.enderecoEntrega.cidade}
                                    /{pedido.enderecoEntrega.uf}
                                </p>
                                <p className="preco">{formatCEP(pedido.enderecoEntrega.cep)}</p>
                            </address>
                        ) : (
                            <p className="py-4 text-sm text-ink-soft">
                                Este pedido não tem endereço de entrega registrado.
                            </p>
                        )}
                        {pedido.observacaoCliente && (
                            <p className="mt-4 border-t border-sand pt-3 text-sm text-ink-soft">
                                Recado de quem comprou: {pedido.observacaoCliente}
                            </p>
                        )}
                    </Cartao>

                    <Cartao titulo="Pagamento">
                        {pedido.pagamento ? (
                            <dl>
                                <Linha rotulo="Situação">
                                    <BadgeStatus
                                        mapa={STATUS_PAGAMENTO}
                                        valor={pedido.pagamento.status}
                                    />
                                </Linha>
                                <Linha rotulo="Provedor">{pedido.pagamento.provedor}</Linha>
                                <Linha rotulo="Método">{pedido.pagamento.metodo}</Linha>
                                <Linha rotulo="Valor">
                                    <span className="preco">
                                        {formatarCentavosParaBRL(pedido.pagamento.valorCentavos)}
                                        {pedido.pagamento.parcelas > 1
                                            ? ` em ${pedido.pagamento.parcelas}x`
                                            : ""}
                                    </span>
                                </Linha>
                                <Linha rotulo="Confirmado em">
                                    {pedido.pagamento.dataConfirmacao
                                        ? formatarDataHora(pedido.pagamento.dataConfirmacao)
                                        : null}
                                </Linha>
                                {pedido.pagamento.paymentUrl && (
                                    <Linha rotulo="Checkout">
                                        <a
                                            href={pedido.pagamento.paymentUrl}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="inline-flex items-center gap-1 underline decoration-sand underline-offset-4 hover:text-ink"
                                        >
                                            Abrir <FiExternalLink size={12} aria-hidden="true" />
                                        </a>
                                    </Linha>
                                )}
                            </dl>
                        ) : (
                            <p className="py-4 text-sm text-ink-soft">
                                Ainda não há tentativa de pagamento neste pedido.
                            </p>
                        )}
                    </Cartao>

                    <Cartao titulo="Envio">
                        {pedido.envio ? (
                            <dl>
                                <Linha rotulo="Situação">
                                    <BadgeStatus mapa={STATUS_ENVIO} valor={pedido.envio.status} />
                                </Linha>
                                <Linha rotulo="Transportadora">
                                    {pedido.envio.transportadora}
                                </Linha>
                                <Linha rotulo="Serviço">{pedido.envio.servico}</Linha>
                                <Linha rotulo="Rastreio">
                                    <span className="preco">{pedido.envio.codigoRastreio}</span>
                                </Linha>
                                <Linha rotulo="Prazo">
                                    {pedido.envio.prazoDias
                                        ? `${pedido.envio.prazoDias} dia(s)`
                                        : null}
                                </Linha>
                                <Linha rotulo="Atualizado">
                                    {pedido.envio.dataAlteracao
                                        ? formatarDataHora(pedido.envio.dataAlteracao)
                                        : null}
                                </Linha>
                            </dl>
                        ) : (
                            <p className="py-4 text-sm text-ink-soft">
                                O envio ainda não foi criado. Ele nasce quando o pagamento é
                                confirmado.
                            </p>
                        )}

                        {problemaEnvio && (
                            <div className="mt-4 border border-danger/40 bg-linen px-3 py-3">
                                <p className="flex items-center gap-2 text-xs uppercase tracking-widest text-danger">
                                    <FiAlertTriangle size={13} aria-hidden="true" />
                                    Envio travado
                                </p>
                                <p className="mt-2 text-xs text-ink-soft">
                                    {problemaEnvio.tentativas} tentativa(s)
                                    {problemaEnvio.proximaTentativaEm
                                        ? ` · próxima ${formatarRelativo(problemaEnvio.proximaTentativaEm)}`
                                        : ""}
                                </p>
                                {problemaEnvio.ultimoErro && (
                                    <p className="mt-1 text-xs text-danger">
                                        {problemaEnvio.ultimoErro}
                                    </p>
                                )}
                            </div>
                        )}

                        <div className="mt-4 border-t border-sand pt-3">
                            <Botao
                                variante="texto"
                                tamanho="sm"
                                onClick={gerarLinkPublico}
                                carregando={gerarLinkPublicoEtiqueta.isPending}
                            >
                                Gerar link aberto da etiqueta
                            </Botao>
                            <p className="mt-1 text-xs text-taupe">
                                Qualquer pessoa com esse endereço abre o PDF. Use só para enviar à
                                transportadora.
                            </p>
                        </div>
                    </Cartao>

                    <Cartao titulo="Datas">
                        <dl>
                            <Linha rotulo="Criado">{formatarDataHora(pedido.dataCriacao)}</Linha>
                            <Linha rotulo="Pago">
                                {pedido.dataPagamento
                                    ? formatarDataHora(pedido.dataPagamento)
                                    : null}
                            </Linha>
                            <Linha rotulo="Enviado">
                                {pedido.dataEnvio ? formatarDataHora(pedido.dataEnvio) : null}
                            </Linha>
                            <Linha rotulo="Entregue">
                                {pedido.dataEntrega ? formatarDataHora(pedido.dataEntrega) : null}
                            </Linha>
                            <Linha rotulo="Cancelado">
                                {pedido.dataCancelamento
                                    ? formatarDataHora(pedido.dataCancelamento)
                                    : null}
                            </Linha>
                        </dl>
                    </Cartao>
                </div>
            </div>

            {/* ------------------------------------------------------- modais */}
            <Modal
                isOpen={modalStatus}
                onClose={() => setModalStatus(false)}
                titulo="Mudar o status do pedido"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setModalStatus(false)}>
                            Voltar
                        </Botao>
                        <Botao
                            onClick={confirmarStatus}
                            disabled={!novoStatus}
                            carregando={alterarStatus.isPending}
                        >
                            Confirmar
                        </Botao>
                    </>
                }
            >
                <Campo
                    label="Novo status"
                    como="select"
                    obrigatorio
                    value={novoStatus}
                    onChange={(e) => setNovoStatus(e.target.value)}
                >
                    <option value="">Escolha o novo status</option>
                    {opcoesStatus.map((s) => (
                        <option key={s.valor} value={s.valor}>
                            {s.rotulo}
                        </option>
                    ))}
                </Campo>

                <Campo
                    label="Observação"
                    como="textarea"
                    containerClassName="mt-4"
                    maxLength={500}
                    value={observacao}
                    ajuda="Fica registrada no histórico. Escreva o que a próxima pessoa precisa saber."
                    onChange={(e) => setObservacao(e.target.value)}
                />

                <p className="mt-4 text-xs text-taupe">
                    Cancelamento não está nesta lista de propósito: ele devolve o estoque e cancela
                    a etiqueta, então tem um botão próprio.
                </p>
            </Modal>

            <Modal
                isOpen={modalCancelar}
                onClose={() => setModalCancelar(false)}
                titulo="Cancelar o pedido"
                largura="sm"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setModalCancelar(false)}>
                            Voltar
                        </Botao>
                        <Botao
                            variante="perigo"
                            onClick={confirmarCancelamento}
                            carregando={cancelar.isPending}
                        >
                            Cancelar pedido
                        </Botao>
                    </>
                }
            >
                <p className="mb-4 text-sm leading-relaxed">
                    O estoque volta para a prateleira conforme o estágio do pedido e a etiqueta é
                    cancelada na transportadora. O pedido continua existindo, marcado como
                    cancelado.
                </p>
                <Campo
                    label="Motivo"
                    como="textarea"
                    obrigatorio
                    maxLength={500}
                    value={motivo}
                    erro={erroMotivo}
                    onChange={(e) => setMotivo(e.target.value)}
                />
            </Modal>

            <Modal
                isOpen={!!linkPublico}
                onClose={() => setLinkPublico(null)}
                titulo="Link aberto da etiqueta"
                largura="md"
                rodape={
                    <Botao variante="contorno" onClick={() => setLinkPublico(null)}>
                        Fechar
                    </Botao>
                }
            >
                <p className="mb-3 text-sm leading-relaxed">
                    Este endereço não pede senha. Compartilhe apenas com quem precisa imprimir a
                    etiqueta.
                </p>
                <p className="break-all border border-sand bg-linen px-3 py-2 text-xs text-ink">
                    {linkPublico}
                </p>
                <a
                    href={linkPublico ?? "#"}
                    target="_blank"
                    rel="noreferrer"
                    className="mt-3 inline-flex items-center gap-1 text-sm underline decoration-sand underline-offset-4 hover:text-ink"
                >
                    Abrir agora <FiExternalLink size={13} aria-hidden="true" />
                </a>
            </Modal>
        </div>
    );
}
