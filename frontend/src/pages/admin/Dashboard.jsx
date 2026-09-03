import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
    FiAlertTriangle,
    FiDollarSign,
    FiMessageSquare,
    FiPackage,
    FiRefreshCw,
    FiShoppingBag,
    FiTruck,
} from "react-icons/fi";

import CartaoMetrica from "@/components/admin/CartaoMetrica.jsx";
import FiltroPeriodo from "@/components/admin/FiltroPeriodo.jsx";
import GraficoBarras from "@/components/admin/GraficoBarras.jsx";
import {
    BlocoSecao,
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
    SkeletonCartoes,
} from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

import { useDashboard } from "@/hooks/useDashboard.js";
import { usePermissoes } from "@/hooks/usePermissoes.js";
import { POLITICAS } from "@/lib/permissoes.js";
import {
    fimDoDiaLocal,
    inicioDoDiaLocal,
    intervaloDoPreset,
    paraInputDateLocal,
    paraParametroUtc,
    PRESET_PADRAO,
} from "@/lib/periodo.js";
import { descrever, STATUS_PEDIDO } from "@/lib/statusAdmin.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { formatarData, formatarDataHora, formatarRelativo } from "@/utils/datas.js";

/**
 * Tela inicial do painel (policy PainelAdmin — todo papel administrativo entra).
 *
 * Duas datas diferentes sustentam esta tela, e a distinção é do backend:
 * faturamento e ranking usam a data de PAGAMENTO (dinheiro que entrou) e
 * "pedidos por status" usa a data de CRIAÇÃO (o que a equipe tem para
 * trabalhar). Por isso a soma das barras de status não bate com o número de
 * pedidos pagos — e o texto abaixo do gráfico diz isso, para ninguém abrir
 * chamado achando que é erro de conta.
 *
 * Os blocos operacionais (estoque, envio, moderação) ignoram o período de
 * propósito: pendência não expira porque o filtro mudou.
 */
export default function Dashboard() {
    const { pode } = usePermissoes();
    const [periodo, setPeriodo] = useState(() => {
        const { de, ate } = intervaloDoPreset(PRESET_PADRAO);
        return {
            preset: PRESET_PADRAO,
            de: paraInputDateLocal(de),
            ate: paraInputDateLocal(ate),
        };
    });

    // O intervalo efetivo: o preset manda, salvo no modo personalizado.
    const parametros = useMemo(() => {
        if (periodo.preset === "personalizado") {
            const de = inicioDoDiaLocal(periodo.de);
            const ate = fimDoDiaLocal(periodo.ate);
            return { de: paraParametroUtc(de), ate: paraParametroUtc(ate) };
        }
        const { de, ate } = intervaloDoPreset(periodo.preset);
        return { de: paraParametroUtc(de), ate: paraParametroUtc(ate) };
    }, [periodo]);

    const { resumo, isLoading, isFetching, isError, refetch } = useDashboard(parametros);

    const barrasStatus = useMemo(
        () =>
            (resumo?.pedidosPorStatus ?? []).map((linha) => {
                const info = descrever(STATUS_PEDIDO, linha.statusNome);
                return {
                    chave: linha.statusNome,
                    rotulo: info.rotulo,
                    valor: linha.quantidade,
                    apoio: formatarCentavosParaBRL(linha.totalCentavos),
                    tom:
                        info.variante === "erro"
                            ? "critico"
                            : info.variante === "alerta"
                              ? "alerta"
                              : "neutro",
                };
            }),
        [resumo],
    );

    const barrasVendidos = useMemo(
        () =>
            (resumo?.produtosMaisVendidos ?? []).map((linha) => ({
                chave: `p-${linha.idProduto}`,
                rotulo: linha.nomeProduto,
                valor: linha.quantidadeVendida,
                apoio: formatarCentavosParaBRL(linha.totalCentavos),
                tom: "acento",
            })),
        [resumo],
    );

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Painel"
                titulo="Visão geral da operação"
                descricao="Faturamento e ranking usam a data de pagamento. Os blocos de pendência abaixo ignoram o período: estoque, envio e moderação mostram sempre a situação de agora."
                acoes={
                    <Botao
                        variante="contorno"
                        tamanho="sm"
                        onClick={() => refetch()}
                        carregando={isFetching}
                    >
                        <FiRefreshCw size={14} aria-hidden="true" />
                        Atualizar
                    </Botao>
                }
            />

            <FiltroPeriodo valor={periodo} onChange={setPeriodo} className="mb-8" />

            {isError && (
                <EstadoErro
                    mensagem="O resumo do painel não pôde ser carregado. Isso não afeta a loja — apenas esta tela."
                    onTentarDeNovo={refetch}
                />
            )}

            {!isError && isLoading && (
                <>
                    <SkeletonCartoes quantidade={4} />
                    <div className="mt-10 grid gap-8 lg:grid-cols-2">
                        <Skeleton className="h-64 w-full" />
                        <Skeleton className="h-64 w-full" />
                    </div>
                </>
            )}

            {!isError && !isLoading && resumo && (
                <>
                    <p className="mb-4 text-xs uppercase tracking-widest text-taupe">
                        {formatarData(resumo.periodoInicio)} — {formatarData(resumo.periodoFim)}
                    </p>

                    <section aria-label="Indicadores do período" className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                        <CartaoMetrica
                            rotulo="Faturamento"
                            valor={formatarCentavosParaBRL(resumo.faturamentoCentavos)}
                            apoio="Soma dos pedidos pagos no período."
                            Icone={FiDollarSign}
                            tom="positivo"
                        />
                        <CartaoMetrica
                            rotulo="Pedidos pagos"
                            valor={resumo.pedidosPagos}
                            apoio="Pagamento confirmado, não pedidos criados."
                            Icone={FiShoppingBag}
                        />
                        <CartaoMetrica
                            rotulo="Ticket médio"
                            valor={formatarCentavosParaBRL(resumo.ticketMedioCentavos)}
                            apoio="Faturamento dividido pelos pedidos pagos."
                            Icone={FiPackage}
                        />
                        <CartaoMetrica
                            rotulo="Desconto concedido"
                            valor={formatarCentavosParaBRL(resumo.descontoConcedidoCentavos)}
                            apoio={`Frete cobrado: ${formatarCentavosParaBRL(resumo.freteCobradoCentavos)}`}
                            Icone={FiDollarSign}
                            tom="alerta"
                        />
                    </section>

                    <section
                        aria-label="Pendências da operação"
                        className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-3"
                    >
                        <CartaoMetrica
                            rotulo="Estoque abaixo do mínimo"
                            valor={resumo.totalEstoqueAbaixoDoMinimo}
                            apoio="SKUs que precisam de reposição."
                            Icone={FiAlertTriangle}
                            tom={resumo.totalEstoqueAbaixoDoMinimo > 0 ? "critico" : "neutro"}
                            to="/admin/estoque"
                        />
                        <CartaoMetrica
                            rotulo="Envios com problema"
                            valor={resumo.totalEnviosComProblema}
                            apoio="Etiquetas em falha ou travadas na fila."
                            Icone={FiTruck}
                            tom={resumo.totalEnviosComProblema > 0 ? "critico" : "neutro"}
                            to="/admin/pedidos"
                        />
                        <CartaoMetrica
                            rotulo="Avaliações pendentes"
                            valor={resumo.avaliacoesPendentes}
                            apoio="Nenhuma chega à vitrine sem moderação."
                            Icone={FiMessageSquare}
                            tom={resumo.avaliacoesPendentes > 0 ? "alerta" : "neutro"}
                            to={pode(POLITICAS.GESTAO_CATALOGO) ? "/admin/avaliacoes" : undefined}
                        />
                    </section>

                    <div className="mt-12 grid gap-10 lg:grid-cols-2">
                        <BlocoSecao
                            titulo="Pedidos por status"
                            descricao="Contados pela data de criação — por isso a soma não fecha com os pedidos pagos acima."
                            className="mb-0"
                        >
                            {barrasStatus.length === 0 ? (
                                <EstadoVazio
                                    titulo="Nenhum pedido no período"
                                    mensagem="Escolha um intervalo maior no seletor acima ou aguarde a primeira venda da janela."
                                />
                            ) : (
                                <GraficoBarras dados={barrasStatus} />
                            )}
                        </BlocoSecao>

                        <BlocoSecao
                            titulo="Mais vendidos"
                            descricao="O nome vem do que ficou congelado no pedido, não do catálogo de hoje."
                            className="mb-0"
                        >
                            {barrasVendidos.length === 0 ? (
                                <EstadoVazio
                                    titulo="Sem ranking no período"
                                    mensagem="O ranking usa pedidos já pagos. Assim que um pagamento for confirmado ele aparece aqui."
                                />
                            ) : (
                                <GraficoBarras
                                    dados={barrasVendidos}
                                    formatarValor={(v) => `${v} un.`}
                                />
                            )}
                        </BlocoSecao>
                    </div>

                    <div className="mt-12 grid gap-10 lg:grid-cols-2">
                        <BlocoSecao
                            titulo="Estoque crítico"
                            descricao="Disponível abaixo do mínimo. Disponível é o físico menos o reservado."
                            acoes={
                                <Link
                                    to="/admin/estoque"
                                    className="font-sans text-xs uppercase tracking-widest text-ink-soft underline decoration-sand underline-offset-4 hover:text-ink"
                                >
                                    Abrir estoque
                                </Link>
                            }
                            className="mb-0"
                        >
                            {resumo.estoqueCritico.length === 0 ? (
                                <EstadoVazio
                                    titulo="Nada em falta"
                                    mensagem="Nenhum SKU está abaixo do mínimo configurado. O limite de cada peça é ajustado na tela de estoque."
                                />
                            ) : (
                                <ul className="divide-y divide-sand border border-sand bg-base-100">
                                    {resumo.estoqueCritico.map((linha) => (
                                        <li
                                            key={linha.idVariacao}
                                            className="flex items-center justify-between gap-4 px-4 py-3"
                                        >
                                            <div className="min-w-0">
                                                <p className="truncate text-sm text-ink">
                                                    {linha.nomeProduto}
                                                </p>
                                                <p className="truncate text-xs text-ink-soft">
                                                    {linha.sku} · {linha.tamanho} · {linha.cor}
                                                </p>
                                            </div>
                                            <div className="shrink-0 text-right">
                                                <p className="preco text-sm text-danger">
                                                    {linha.disponivel} disp.
                                                </p>
                                                <p className="preco text-xs text-ink-soft">
                                                    mín. {linha.quantidadeMinima} · res.{" "}
                                                    {linha.quantidadeReservada}
                                                </p>
                                            </div>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </BlocoSecao>

                        <BlocoSecao
                            titulo="Fila de envio travada"
                            descricao="Entra aqui o envio em falha e também o que já tentou uma vez e continua pendente."
                            className="mb-0"
                        >
                            {resumo.filaEnvioComProblema.length === 0 ? (
                                <EstadoVazio
                                    titulo="Fila limpa"
                                    mensagem="Nenhuma etiqueta travada. As que falharem aparecem aqui antes de o cliente cobrar o rastreio."
                                />
                            ) : (
                                <ul className="divide-y divide-sand border border-sand bg-base-100">
                                    {resumo.filaEnvioComProblema.map((linha) => (
                                        <li key={linha.idEnvio} className="px-4 py-3">
                                            <div className="flex flex-wrap items-center justify-between gap-2">
                                                <span className="preco text-sm text-ink">
                                                    {linha.numeroPedido}
                                                </span>
                                                <Badge variante="erro">{linha.statusNome}</Badge>
                                            </div>
                                            <p className="mt-1 text-xs text-ink-soft">
                                                {linha.tentativas} tentativa(s)
                                                {linha.proximaTentativaEm
                                                    ? ` · próxima ${formatarRelativo(linha.proximaTentativaEm)}`
                                                    : ""}
                                            </p>
                                            {linha.ultimoErro && (
                                                <p className="mt-1 line-clamp-2 text-xs text-danger">
                                                    {linha.ultimoErro}
                                                </p>
                                            )}
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </BlocoSecao>
                    </div>

                    <p className="mt-10 text-xs text-taupe">
                        Atualizado em {formatarDataHora(new Date())}.
                    </p>
                </>
            )}
        </div>
    );
}
