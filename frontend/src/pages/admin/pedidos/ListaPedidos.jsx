import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FiRefreshCw, FiSearch, FiShoppingBag } from "react-icons/fi";

import BadgeStatus from "@/components/admin/BadgeStatus.jsx";
import FiltroPeriodo from "@/components/admin/FiltroPeriodo.jsx";
import {
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
} from "@/components/admin/EstadoConsulta.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import { usePedidosAdmin } from "@/hooks/usePedidosAdmin.js";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";
import {
    fimDoDiaLocal,
    inicioDoDiaLocal,
    intervaloDoPreset,
    paraParametroUtc,
} from "@/lib/periodo.js";
import { STATUS_PEDIDO } from "@/lib/statusAdmin.js";
import { formatarDataHora } from "@/utils/datas.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

const PERIODO_INICIAL = { preset: "90dias", de: "", ate: "" };

/**
 * Fila de trabalho da expedição (policy Expedicao).
 *
 * Paginação SERVER-SIDE: `page` e `pageSize` entram no filtro e, portanto, na
 * chave do React Query. Não existe ordenação por coluna aqui porque o endpoint
 * não aceita `sort` — cabeçalho clicável que não ordena de verdade é pior do
 * que cabeçalho fixo.
 *
 * A busca casa por número do pedido OU nome do destinatário; o resumo da
 * listagem não traz o nome do cliente, então ele não vira coluna.
 */
export default function ListaPedidos() {
    const navegar = useNavigate();

    const [status, setStatus] = useState("");
    const [buscaDigitada, setBuscaDigitada] = useState("");
    const [busca, setBusca] = useState("");
    const [periodo, setPeriodo] = useState(PERIODO_INICIAL);
    const [pagina, setPagina] = useState(1);

    // Espera o operador parar de digitar antes de consultar. Sem isso, cada
    // tecla de um número de pedido dispara uma consulta paginada.
    useEffect(() => {
        const t = setTimeout(() => setBusca(buscaDigitada.trim()), 400);
        return () => clearTimeout(t);
    }, [buscaDigitada]);

    // Qualquer mudança de filtro devolve para a primeira página: manter a
    // página 7 depois de filtrar mostra "nenhum registro" numa lista cheia.
    useEffect(() => {
        setPagina(1);
    }, [status, busca, periodo]);

    const intervalo = useMemo(() => {
        if (periodo.preset === "personalizado") {
            return {
                de: paraParametroUtc(inicioDoDiaLocal(periodo.de)),
                ate: paraParametroUtc(fimDoDiaLocal(periodo.ate)),
            };
        }
        const { de, ate } = intervaloDoPreset(periodo.preset);
        return { de: paraParametroUtc(de), ate: paraParametroUtc(ate) };
    }, [periodo]);

    const filtros = useMemo(
        () => ({
            status: status || undefined,
            busca: busca || undefined,
            de: intervalo.de,
            ate: intervalo.ate,
            page: pagina,
            pageSize: ITENS_POR_PAGINA,
        }),
        [status, busca, intervalo, pagina],
    );

    const {
        pedidos,
        total,
        totalPaginas,
        tamanhoPagina,
        isLoading,
        isFetching,
        isError,
        refetch,
    } = usePedidosAdmin(filtros);

    const temFiltro = !!status || !!busca || periodo.preset !== PERIODO_INICIAL.preset;

    const colunas = [
        {
            chave: "numeroPedido",
            titulo: "Pedido",
            render: (linha) => (
                <div className="min-w-0">
                    <p className="preco text-sm text-ink">{linha.numero}</p>
                    <p className="text-xs text-ink-soft">{formatarDataHora(linha.dataCriacao)}</p>
                </div>
            ),
        },
        {
            chave: "quantidadeItens",
            titulo: "Itens",
            alinhamento: "centro",
            render: (linha) => <span className="preco text-sm">{linha.quantidadeItens}</span>,
        },
        {
            chave: "situacaoPedido",
            titulo: "Status",
            render: (linha) => <BadgeStatus mapa={STATUS_PEDIDO} valor={linha.status} />,
        },
        {
            chave: "pagoEm",
            titulo: "Pagamento",
            render: (linha) => (
                <span className="text-xs text-ink-soft">
                    {linha.dataPagamento ? formatarDataHora(linha.dataPagamento) : "—"}
                </span>
            ),
        },
        {
            chave: "rastreio",
            titulo: "Rastreio",
            render: (linha) => (
                <span className="preco text-xs text-ink-soft">
                    {linha.codigoRastreio || "—"}
                </span>
            ),
        },
        {
            chave: "totalPedido",
            titulo: "Total",
            alinhamento: "direita",
            render: (linha) => (
                <span className="preco text-sm text-ink">
                    {formatarCentavosParaBRL(linha.totalCentavos)}
                </span>
            ),
        },
    ];

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Operação"
                titulo="Pedidos"
                descricao="Fila de expedição. Clique em uma linha para abrir o pedido, mudar o status, emitir a etiqueta ou acompanhar o rastreio."
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

            <div className="mb-6 flex flex-col gap-4 border border-sand bg-linen p-4">
                <div className="flex flex-wrap items-end gap-4">
                    <Campo
                        label="Buscar"
                        placeholder="Número do pedido ou nome de quem recebe"
                        value={buscaDigitada}
                        onChange={(e) => setBuscaDigitada(e.target.value)}
                        containerClassName="min-w-[16rem] flex-1"
                        maxLength={120}
                    />

                    <Campo
                        label="Status"
                        como="select"
                        value={status}
                        onChange={(e) => setStatus(e.target.value)}
                        containerClassName="w-56"
                    >
                        <option value="">Todos os status</option>
                        {STATUS_PEDIDO.map((s) => (
                            <option key={s.valor} value={s.valor}>
                                {s.rotulo}
                            </option>
                        ))}
                    </Campo>
                </div>

                <FiltroPeriodo valor={periodo} onChange={setPeriodo} />

                {temFiltro && (
                    <div>
                        <Botao
                            variante="texto"
                            tamanho="sm"
                            onClick={() => {
                                setStatus("");
                                setBuscaDigitada("");
                                setPeriodo(PERIODO_INICIAL);
                            }}
                        >
                            Limpar filtros
                        </Botao>
                    </div>
                )}
            </div>

            {isError ? (
                <EstadoErro
                    mensagem="A fila de pedidos não pôde ser carregada."
                    onTentarDeNovo={refetch}
                />
            ) : !isLoading && pedidos.length === 0 ? (
                <EstadoVazio
                    Icone={FiSearch}
                    titulo={temFiltro ? "Nenhum pedido com esses filtros" : "Nenhum pedido ainda"}
                    mensagem={
                        temFiltro
                            ? "Amplie o período ou limpe o status para ver a fila inteira."
                            : "Assim que a loja receber a primeira venda ela aparece aqui, pronta para separar."
                    }
                    acao={
                        temFiltro ? (
                            <Botao
                                variante="contorno"
                                tamanho="sm"
                                onClick={() => {
                                    setStatus("");
                                    setBuscaDigitada("");
                                    setPeriodo(PERIODO_INICIAL);
                                }}
                            >
                                Limpar filtros
                            </Botao>
                        ) : null
                    }
                />
            ) : (
                <>
                    <Tabela
                        colunas={colunas}
                        dados={pedidos}
                        carregando={isLoading}
                        chaveLinha={(linha) => linha.uuid}
                        onLinhaClick={(linha) => navegar(`/admin/pedidos/${linha.uuid}`)}
                        vazio="Nenhum pedido nesta página."
                    />

                    <Paginacao
                        className="mt-6"
                        paginaAtual={pagina}
                        totalPaginas={totalPaginas}
                        totalItens={total}
                        itensPorPagina={tamanhoPagina}
                        onMudarPagina={setPagina}
                    />
                </>
            )}

            {!isError && !isLoading && pedidos.length > 0 && (
                <p className="mt-6 flex items-center gap-2 text-xs text-taupe">
                    <FiShoppingBag size={13} aria-hidden="true" />
                    {total} pedido(s) no período selecionado.
                </p>
            )}
        </div>
    );
}
