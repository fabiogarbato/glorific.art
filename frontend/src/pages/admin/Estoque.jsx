import { useEffect, useMemo, useState } from "react";
import {
    FiAlertTriangle,
    FiArchive,
    FiPlusCircle,
    FiRefreshCw,
    FiSearch,
    FiSliders,
} from "react-icons/fi";

import {
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
} from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import {
    useAcoesEstoque,
    useAlertaMinimo,
    useMovimentacoes,
    useProdutosParaEstoque,
    useVariacoesDoProduto,
} from "@/hooks/useEstoqueAdmin.js";
import { usePermissoes } from "@/hooks/usePermissoes.js";
import { POLITICAS } from "@/lib/permissoes.js";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";
import {
    MOVIMENTOS_AJUSTE,
    MOVIMENTOS_ENTRADA,
    MOVIMENTOS_ESTOQUE,
} from "@/lib/statusAdmin.js";
import { formatarDataHora } from "@/utils/datas.js";

/**
 * Estoque por variação (policy Expedicao — quem conta prateleira é a expedição).
 *
 * Os três números aparecem sempre juntos: físico é o que existe, reservado é o
 * que já está comprometido em checkout aguardando pagamento e disponível é o
 * único que pode ser vendido. Mostrar um só é o que faz alguém achar que há
 * peça quando ela já foi vendida.
 *
 * Não existe rota "listar todo o estoque paginado" no backend, e isso é
 * intencional: o relatório de reposição é uma lista de ação, curta por
 * definição. Por isso a tela tem três abas — a fila do que falta, a grade de um
 * produto escolhido e o extrato do razão.
 */

/**
 * Barra de saúde de estoque (2px sob a linha): olive confortável, brass perto
 * do mínimo, danger esgotado. Le direto do disponivel/minimo já normalizados.
 */
function corSaudeEstoque(l) {
    if (l.disponivel <= 0) return "border-b-2 border-danger";
    if (l.abaixoDoMinimo) return "border-b-2 border-brass";
    return "border-b-2 border-olive";
}

/** As duas origens de saldo têm nomes de campo diferentes. Uma forma só aqui. */
function normalizarLinha(bruto, produto) {
    // EstoqueVariacaoResponseDto (alerta de mínimo)
    if (bruto.idVariacao != null) {
        return {
            idVariacao: bruto.idVariacao,
            sku: bruto.sku,
            nomeProduto: bruto.nomeProduto,
            tamanho: bruto.tamanho,
            cor: bruto.cor,
            fisico: bruto.quantidade,
            reservado: bruto.quantidadeReservada,
            disponivel: bruto.disponivel,
            minimo: bruto.quantidadeMinima,
            localizacao: bruto.localizacao,
            ultimaMovimentacao: bruto.dataUltimaMovimentacao,
            abaixoDoMinimo: bruto.abaixoDoMinimo ?? bruto.disponivel < bruto.quantidadeMinima,
        };
    }

    // ProdutoVariacaoResponseDto (grade do produto)
    return {
        idVariacao: bruto.id,
        sku: bruto.sku,
        nomeProduto: produto?.nome ?? "",
        tamanho: bruto.codigoTamanho,
        cor: bruto.nomeCor,
        fisico: bruto.quantidadeEmEstoque,
        reservado: bruto.quantidadeReservada,
        disponivel: bruto.quantidadeDisponivel,
        minimo: bruto.quantidadeMinima,
        localizacao: null,
        ultimaMovimentacao: null,
        abaixoDoMinimo: bruto.quantidadeDisponivel < bruto.quantidadeMinima,
        inativo: bruto.ativo === false,
    };
}

export default function Estoque() {
    const { pode } = usePermissoes();
    const podeVerCatalogo = pode(POLITICAS.GESTAO_CATALOGO);

    const [aba, setAba] = useState("alerta");

    // ------------------------------------------------------------- modais
    const [ajuste, setAjuste] = useState(null);
    const [entrada, setEntrada] = useState(null);
    const [parametros, setParametros] = useState(null);

    const { ajustar, registrarEntrada, atualizarParametros } = useAcoesEstoque();

    // ---------------------------------------------------------- aba alerta
    const alerta = useAlertaMinimo({ habilitado: aba === "alerta" });

    // --------------------------------------------------------- aba produto
    const [buscaDigitada, setBuscaDigitada] = useState("");
    const [buscaProduto, setBuscaProduto] = useState("");
    const [paginaProduto, setPaginaProduto] = useState(1);
    const [produtoSelecionado, setProdutoSelecionado] = useState(null);

    useEffect(() => {
        const t = setTimeout(() => setBuscaProduto(buscaDigitada.trim()), 400);
        return () => clearTimeout(t);
    }, [buscaDigitada]);

    useEffect(() => {
        setPaginaProduto(1);
    }, [buscaProduto]);

    const listaProdutos = useProdutosParaEstoque(
        { q: buscaProduto || undefined, ativo: undefined, page: paginaProduto, pageSize: 10 },
        { habilitado: aba === "produto" && podeVerCatalogo },
    );

    const grade = useVariacoesDoProduto(aba === "produto" ? produtoSelecionado?.id : null);

    // ------------------------------------------------------- aba extrato
    const [filtroMovimento, setFiltroMovimento] = useState("");
    const [filtroSku, setFiltroSku] = useState("");
    const [paginaExtrato, setPaginaExtrato] = useState(1);

    useEffect(() => {
        setPaginaExtrato(1);
    }, [filtroMovimento, filtroSku]);

    const extrato = useMovimentacoes(
        {
            movimento: filtroMovimento || undefined,
            idVariacao: filtroSku ? Number(filtroSku) : undefined,
            page: paginaExtrato,
            pageSize: ITENS_POR_PAGINA,
        },
        { habilitado: aba === "extrato" },
    );

    const linhasAlerta = useMemo(
        () => (alerta.criticos ?? []).map((linha) => normalizarLinha(linha)),
        [alerta.criticos],
    );

    const linhasGrade = useMemo(
        () => (grade.variacoes ?? []).map((v) => normalizarLinha(v, produtoSelecionado)),
        [grade.variacoes, produtoSelecionado],
    );

    const colunasSaldo = [
        {
            chave: "skuVariacao",
            titulo: "SKU",
            render: (l) => (
                <div className="min-w-0">
                    <p className="preco text-sm text-ink">{l.sku}</p>
                    <p className="truncate text-xs text-ink-soft">
                        {l.nomeProduto} · {l.tamanho} · {l.cor}
                    </p>
                </div>
            ),
        },
        {
            chave: "saldoFisico",
            titulo: "Físico",
            alinhamento: "direita",
            render: (l) => <span className="preco text-sm">{l.fisico}</span>,
        },
        {
            chave: "saldoReservado",
            titulo: "Reservado",
            alinhamento: "direita",
            render: (l) => <span className="preco text-sm text-ink-soft">{l.reservado}</span>,
        },
        {
            chave: "saldoDisponivel",
            titulo: "Disponível",
            alinhamento: "direita",
            render: (l) => (
                <span
                    className={`preco text-sm ${l.abaixoDoMinimo ? "text-danger" : "text-ink"}`}
                >
                    {l.disponivel}
                </span>
            ),
        },
        {
            chave: "saldoMinimo",
            titulo: "Mínimo",
            alinhamento: "direita",
            render: (l) => <span className="preco text-sm text-ink-soft">{l.minimo}</span>,
        },
        {
            chave: "situacaoSaldo",
            titulo: "Situação",
            render: (l) =>
                l.abaixoDoMinimo ? (
                    <Badge variante="erro">Repor</Badge>
                ) : l.inativo ? (
                    <Badge variante="esgotado">Inativa</Badge>
                ) : (
                    <Badge variante="neutro">Em dia</Badge>
                ),
        },
        {
            chave: "acoesSaldo",
            titulo: "",
            alinhamento: "direita",
            render: (l) => (
                <div className="flex justify-end gap-1">
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() => setEntrada({ linha: l, quantidade: "", movimento: MOVIMENTOS_ENTRADA[0].valor, observacao: "" })}
                    >
                        Entrada
                    </Botao>
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() =>
                            setAjuste({
                                linha: l,
                                quantidadeContada: String(l.fisico),
                                movimento: MOVIMENTOS_AJUSTE[0].valor,
                                observacao: "",
                                erro: "",
                            })
                        }
                    >
                        Ajustar
                    </Botao>
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() =>
                            setParametros({
                                linha: l,
                                quantidadeMinima: String(l.minimo),
                                localizacao: l.localizacao ?? "",
                            })
                        }
                    >
                        <FiSliders size={13} aria-hidden="true" />
                        <span className="sr-only">Parâmetros do SKU {l.sku}</span>
                    </Botao>
                </div>
            ),
        },
    ];

    const colunasExtrato = [
        {
            chave: "quandoMovimento",
            titulo: "Quando",
            render: (l) => (
                <span className="text-xs text-ink-soft">
                    {formatarDataHora(l.dataMovimentacao)}
                </span>
            ),
        },
        {
            chave: "skuMovimento",
            titulo: "SKU",
            render: (l) => (
                <div className="min-w-0">
                    <p className="preco text-sm text-ink">{l.sku || l.idVariacao}</p>
                    <p className="truncate text-xs text-ink-soft">{l.nomeProduto}</p>
                </div>
            ),
        },
        { chave: "tipoMovimento", titulo: "Movimento", render: (l) => l.movimento },
        {
            chave: "quantidadeMovimento",
            titulo: "Quantidade",
            alinhamento: "direita",
            render: (l) => (
                <span className={`preco text-sm ${l.quantidade < 0 ? "text-danger" : "text-olive"}`}>
                    {l.quantidade > 0 ? `+${l.quantidade}` : l.quantidade}
                </span>
            ),
        },
        {
            chave: "saldoMovimento",
            titulo: "Antes → depois",
            alinhamento: "direita",
            render: (l) => (
                <span className="preco text-xs text-ink-soft">
                    {l.quantidadeAntes} → {l.quantidadeDepois}
                </span>
            ),
        },
        {
            chave: "notaMovimento",
            titulo: "Observação",
            render: (l) => (
                <span className="text-xs text-ink-soft">
                    {l.observacao || (l.idPedido ? `Pedido ${l.idPedido}` : "—")}
                </span>
            ),
        },
    ];

    const abas = [
        { chave: "alerta", rotulo: "Abaixo do mínimo" },
        ...(podeVerCatalogo ? [{ chave: "produto", rotulo: "Por produto" }] : []),
        { chave: "extrato", rotulo: "Movimentação" },
    ];

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Operação"
                titulo="Estoque"
                descricao="Disponível é o físico menos o reservado — é o único número que a vitrine pode vender. Entrada e ajuste ficam registrados no extrato, com quem lançou e por quê."
                acoes={
                    aba === "alerta" ? (
                        <Botao
                            variante="contorno"
                            tamanho="sm"
                            onClick={() => alerta.refetch()}
                            carregando={alerta.isFetching}
                        >
                            <FiRefreshCw size={14} aria-hidden="true" />
                            Atualizar
                        </Botao>
                    ) : null
                }
            />

            <div role="tablist" aria-label="Visões do estoque" className="mb-6 flex flex-wrap border border-sand">
                {abas.map((a) => (
                    <button
                        key={a.chave}
                        type="button"
                        role="tab"
                        aria-selected={aba === a.chave}
                        onClick={() => setAba(a.chave)}
                        className={`h-10 px-4 font-sans text-[11px] uppercase tracking-widest transition-colors ${
                            aba === a.chave
                                ? "bg-olive text-bone"
                                : "bg-base-100 text-ink-soft hover:bg-linen hover:text-ink"
                        }`}
                    >
                        {a.rotulo}
                    </button>
                ))}
            </div>

            {/* ------------------------------------------------------ alerta */}
            {aba === "alerta" &&
                (alerta.isError ? (
                    <EstadoErro
                        mensagem="O relatório de reposição não pôde ser carregado."
                        onTentarDeNovo={alerta.refetch}
                    />
                ) : !alerta.isLoading && linhasAlerta.length === 0 ? (
                    <EstadoVazio
                        Icone={FiArchive}
                        titulo="Nada para repor"
                        mensagem="Nenhum SKU está com o disponível abaixo do mínimo. O limite de cada peça é ajustado no botão de parâmetros, na aba por produto."
                    />
                ) : (
                    <>
                        <p className="mb-3 flex items-center gap-2 text-xs uppercase tracking-widest text-danger">
                            <FiAlertTriangle size={13} aria-hidden="true" />
                            {linhasAlerta.length} SKU(s) precisando de reposição
                        </p>
                        <Tabela
                            colunas={colunasSaldo}
                            dados={linhasAlerta}
                            carregando={alerta.isLoading}
                            chaveLinha={(l) => l.idVariacao}
                            classeLinha={corSaudeEstoque}
                            vazio="Nenhum SKU abaixo do mínimo."
                        />
                    </>
                ))}

            {/* ----------------------------------------------------- produto */}
            {aba === "produto" && (
                <div className="grid gap-6 lg:grid-cols-[20rem_1fr]">
                    <div>
                        <Campo
                            label="Buscar peça"
                            placeholder="Nome ou SKU base"
                            value={buscaDigitada}
                            onChange={(e) => setBuscaDigitada(e.target.value)}
                        />

                        <div className="mt-4 border border-sand bg-base-100">
                            {listaProdutos.isLoading ? (
                                <p className="px-4 py-6 text-sm text-ink-soft">Carregando…</p>
                            ) : listaProdutos.produtos.length === 0 ? (
                                <p className="px-4 py-6 text-sm text-ink-soft">
                                    Nenhuma peça com esse termo. Tente parte do nome.
                                </p>
                            ) : (
                                <ul className="divide-y divide-sand/60">
                                    {listaProdutos.produtos.map((p) => (
                                        <li key={p.id}>
                                            <button
                                                type="button"
                                                onClick={() => setProdutoSelecionado(p)}
                                                aria-current={
                                                    produtoSelecionado?.id === p.id
                                                        ? "true"
                                                        : undefined
                                                }
                                                className={`block w-full px-4 py-3 text-left transition-colors ${
                                                    produtoSelecionado?.id === p.id
                                                        ? "bg-linen"
                                                        : "hover:bg-linen/60"
                                                }`}
                                            >
                                                <p className="truncate text-sm text-ink">{p.nome}</p>
                                                <p className="preco truncate text-xs text-ink-soft">
                                                    {p.skuBase} · {p.totalVariacoes} SKU(s) ·{" "}
                                                    {p.estoqueTotalDisponivel} disp.
                                                </p>
                                            </button>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </div>

                        <Paginacao
                            containerClassName="mt-4"
                            paginaAtual={listaProdutos.pagina}
                            totalPaginas={listaProdutos.totalPaginas}
                            onMudarPagina={setPaginaProduto}
                        />
                    </div>

                    <div>
                        {!produtoSelecionado ? (
                            <EstadoVazio
                                Icone={FiSearch}
                                titulo="Escolha uma peça"
                                mensagem="A grade aparece aqui com físico, reservado, disponível e mínimo de cada tamanho e cor."
                            />
                        ) : grade.isError ? (
                            <EstadoErro mensagem="A grade desta peça não pôde ser carregada." />
                        ) : (
                            <>
                                <h2 className="mb-3 font-display text-xl tracking-tight text-ink">
                                    {produtoSelecionado.nome}
                                </h2>
                                <Tabela
                                    colunas={colunasSaldo}
                                    dados={linhasGrade}
                                    carregando={grade.isLoading}
                                    chaveLinha={(l) => l.idVariacao}
                                    classeLinha={corSaudeEstoque}
                                    vazio="Esta peça ainda não tem variações cadastradas."
                                />
                            </>
                        )}
                    </div>
                </div>
            )}

            {/* ----------------------------------------------------- extrato */}
            {aba === "extrato" && (
                <>
                    <div className="mb-6 flex flex-wrap items-end gap-4 border border-sand bg-linen p-4">
                        <Campo
                            label="Movimento"
                            como="select"
                            value={filtroMovimento}
                            onChange={(e) => setFiltroMovimento(e.target.value)}
                            containerClassName="w-60"
                        >
                            <option value="">Todos os movimentos</option>
                            {MOVIMENTOS_ESTOQUE.map((m) => (
                                <option key={m.valor} value={m.valor}>
                                    {m.rotulo}
                                </option>
                            ))}
                        </Campo>

                        <Campo
                            label="Variação"
                            type="number"
                            min="1"
                            placeholder="Identificador da variação"
                            value={filtroSku}
                            onChange={(e) => setFiltroSku(e.target.value)}
                            containerClassName="w-56"
                            ajuda="Deixe em branco para ver todas."
                        />
                    </div>

                    {extrato.isError ? (
                        <EstadoErro mensagem="O extrato não pôde ser carregado." />
                    ) : !extrato.isLoading && extrato.movimentacoes.length === 0 ? (
                        <EstadoVazio
                            titulo="Nenhuma movimentação"
                            mensagem="O razão é criado a cada entrada, venda ou ajuste. Sem lançamento no período filtrado, ele fica assim."
                        />
                    ) : (
                        <>
                            <Tabela
                                colunas={colunasExtrato}
                                dados={extrato.movimentacoes}
                                carregando={extrato.isLoading}
                                chaveLinha={(l) => l.id}
                                vazio="Nenhum lançamento nesta página."
                            />
                            <Paginacao
                                className="mt-6"
                                paginaAtual={extrato.pagina}
                                totalPaginas={extrato.totalPaginas}
                                totalItens={extrato.total}
                                itensPorPagina={extrato.tamanhoPagina}
                                onMudarPagina={setPaginaExtrato}
                            />
                        </>
                    )}
                </>
            )}

            {/* ------------------------------------------------------ modais */}
            <Modal
                isOpen={!!ajuste}
                onClose={() => setAjuste(null)}
                titulo="Ajuste de inventário"
                largura="sm"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setAjuste(null)}>
                            Voltar
                        </Botao>
                        <Botao
                            carregando={ajustar.isPending}
                            onClick={() => {
                                const nota = ajuste.observacao.trim();
                                if (nota.length < 3) {
                                    setAjuste({
                                        ...ajuste,
                                        erro: "Descreva o motivo com pelo menos 3 caracteres.",
                                    });
                                    return;
                                }
                                ajustar.mutate(
                                    {
                                        idVariacao: ajuste.linha.idVariacao,
                                        quantidadeContada: Number(ajuste.quantidadeContada),
                                        movimento: ajuste.movimento,
                                        observacao: nota,
                                    },
                                    { onSuccess: () => setAjuste(null) },
                                );
                            }}
                        >
                            Registrar ajuste
                        </Botao>
                    </>
                }
            >
                {ajuste && (
                    <>
                        <p className="mb-4 text-sm leading-relaxed">
                            Informe a quantidade que você <strong>contou na prateleira</strong>, não
                            a diferença. O sistema calcula o movimento e grava o antes e o depois.
                        </p>
                        <p className="preco mb-4 border border-sand bg-linen px-3 py-2 text-xs text-ink">
                            {ajuste.linha.sku} · físico atual {ajuste.linha.fisico} · reservado{" "}
                            {ajuste.linha.reservado}
                        </p>

                        <Campo
                            label="Quantidade contada"
                            type="number"
                            min="0"
                            obrigatorio
                            value={ajuste.quantidadeContada}
                            onChange={(e) =>
                                setAjuste({ ...ajuste, quantidadeContada: e.target.value })
                            }
                        />

                        <Campo
                            label="Movimento"
                            como="select"
                            containerClassName="mt-4"
                            value={ajuste.movimento}
                            onChange={(e) => setAjuste({ ...ajuste, movimento: e.target.value })}
                        >
                            {MOVIMENTOS_AJUSTE.map((m) => (
                                <option key={m.valor} value={m.valor}>
                                    {m.rotulo}
                                </option>
                            ))}
                        </Campo>

                        <Campo
                            label="Motivo"
                            como="textarea"
                            obrigatorio
                            maxLength={500}
                            containerClassName="mt-4"
                            erro={ajuste.erro}
                            value={ajuste.observacao}
                            ajuda="Fica no extrato para sempre. Escreva o que aconteceu."
                            onChange={(e) =>
                                setAjuste({ ...ajuste, observacao: e.target.value, erro: "" })
                            }
                        />

                        <p className="mt-4 text-xs text-taupe">
                            Redução que invadiria o estoque reservado é recusada pelo servidor: ela
                            derrubaria um pedido já pago.
                        </p>
                    </>
                )}
            </Modal>

            <Modal
                isOpen={!!entrada}
                onClose={() => setEntrada(null)}
                titulo="Entrada de estoque"
                largura="sm"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEntrada(null)}>
                            Voltar
                        </Botao>
                        <Botao
                            carregando={registrarEntrada.isPending}
                            disabled={!Number(entrada?.quantidade)}
                            onClick={() =>
                                registrarEntrada.mutate(
                                    {
                                        itens: [
                                            {
                                                idVariacao: entrada.linha.idVariacao,
                                                quantidade: Number(entrada.quantidade),
                                            },
                                        ],
                                        movimento: entrada.movimento,
                                        observacao: entrada.observacao,
                                    },
                                    { onSuccess: () => setEntrada(null) },
                                )
                            }
                        >
                            <FiPlusCircle size={14} aria-hidden="true" /> Lançar entrada
                        </Botao>
                    </>
                }
            >
                {entrada && (
                    <>
                        <p className="preco mb-4 border border-sand bg-linen px-3 py-2 text-xs text-ink">
                            {entrada.linha.sku} · físico atual {entrada.linha.fisico}
                        </p>

                        <Campo
                            label="Quantidade que entrou"
                            type="number"
                            min="1"
                            obrigatorio
                            value={entrada.quantidade}
                            onChange={(e) => setEntrada({ ...entrada, quantidade: e.target.value })}
                        />

                        <Campo
                            label="Origem"
                            como="select"
                            containerClassName="mt-4"
                            value={entrada.movimento}
                            onChange={(e) => setEntrada({ ...entrada, movimento: e.target.value })}
                        >
                            {MOVIMENTOS_ENTRADA.map((m) => (
                                <option key={m.valor} value={m.valor}>
                                    {m.rotulo}
                                </option>
                            ))}
                        </Campo>

                        <Campo
                            label="Observação"
                            como="textarea"
                            maxLength={500}
                            containerClassName="mt-4"
                            value={entrada.observacao}
                            ajuda="Número da nota do fornecedor, por exemplo."
                            onChange={(e) => setEntrada({ ...entrada, observacao: e.target.value })}
                        />
                    </>
                )}
            </Modal>

            <Modal
                isOpen={!!parametros}
                onClose={() => setParametros(null)}
                titulo="Parâmetros do SKU"
                largura="sm"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setParametros(null)}>
                            Voltar
                        </Botao>
                        <Botao
                            carregando={atualizarParametros.isPending}
                            onClick={() =>
                                atualizarParametros.mutate(
                                    {
                                        idVariacao: parametros.linha.idVariacao,
                                        quantidadeMinima: Number(parametros.quantidadeMinima),
                                        localizacao: parametros.localizacao,
                                    },
                                    { onSuccess: () => setParametros(null) },
                                )
                            }
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                {parametros && (
                    <>
                        <p className="mb-4 text-sm leading-relaxed">
                            Isto não mexe em saldo. Saldo só muda por entrada, venda ou ajuste.
                        </p>

                        <Campo
                            label="Mínimo de alerta"
                            type="number"
                            min="0"
                            value={parametros.quantidadeMinima}
                            ajuda="Quando o disponível cair abaixo deste número, o SKU entra na lista de reposição."
                            onChange={(e) =>
                                setParametros({ ...parametros, quantidadeMinima: e.target.value })
                            }
                        />

                        <Campo
                            label="Localização física"
                            containerClassName="mt-4"
                            maxLength={100}
                            value={parametros.localizacao}
                            ajuda="Prateleira, caixa, corredor — o que ajuda a achar a peça."
                            onChange={(e) =>
                                setParametros({ ...parametros, localizacao: e.target.value })
                            }
                        />
                    </>
                )}
            </Modal>
        </div>
    );
}
