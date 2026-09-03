import { useEffect, useMemo, useState } from "react";
import { FiBarChart2, FiPlus, FiSearch } from "react-icons/fi";

import BadgeStatus from "@/components/admin/BadgeStatus.jsx";
import {
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
} from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import { useAcoesCupom, useCupons, useUsosDoCupom } from "@/hooks/useCupons.js";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";
import { fimDoDiaLocal, inicioDoDiaLocal, paraParametroUtc } from "@/lib/periodo.js";
import {
    TIPO_CUPOM,
    TIPO_CUPOM_FRETE_GRATIS,
    TIPO_CUPOM_PERCENTUAL,
    TIPO_CUPOM_VALOR_FIXO,
} from "@/lib/statusAdmin.js";
import { formatarData, formatarDataHora, paraInputDate } from "@/utils/datas.js";
import {
    formatarCentavosParaBRL,
    mascaraPrecoCentavos,
    parseBRLParaCentavos,
} from "@/utils/financeiro.js";

/**
 * CRUD de cupom (policy GestaoCatalogo).
 *
 * O campo `valor` é polimórfico e essa é a única armadilha real da tela:
 * percentual viaja multiplicado por 100 (1250 = 12,50%) e valor fixo viaja em
 * centavos. O formulário troca o rótulo, a máscara e a conversão junto com o
 * tipo — nunca deixa o operador digitar "10" sem saber se são dez por cento ou
 * dez centavos.
 *
 * `usosAtuais` é só leitura: o contador é escrito por atualização condicional
 * atômica no servidor e mandá-lo de volta reabriria a corrida que aquele
 * mecanismo existe para fechar.
 */

const FORMULARIO_VAZIO = {
    id: null,
    codigo: "",
    descricao: "",
    tipo: TIPO_CUPOM_PERCENTUAL,
    percentual: "",
    valorFixo: "",
    valorMinimo: "",
    descontoMaximo: "",
    usoMaximoTotal: "",
    usoMaximoPorUsuario: "1",
    vigenciaInicio: "",
    vigenciaFim: "",
    primeiraCompraApenas: false,
    idCategoriaRestrita: "",
    idColecaoRestrita: "",
    ativo: true,
};

/** Registro vindo da API -> estado do formulário. */
function paraFormulario(cupom) {
    return {
        id: cupom.id,
        codigo: cupom.codigo ?? "",
        descricao: cupom.descricao ?? "",
        tipo: cupom.tipo,
        percentual: cupom.tipo === TIPO_CUPOM_PERCENTUAL ? String(cupom.valor / 100) : "",
        valorFixo:
            cupom.tipo === TIPO_CUPOM_VALOR_FIXO ? mascaraPrecoCentavos(String(cupom.valor)) : "",
        valorMinimo: cupom.valorMinimoPedidoCentavos
            ? mascaraPrecoCentavos(String(cupom.valorMinimoPedidoCentavos))
            : "",
        descontoMaximo: cupom.descontoMaximoCentavos
            ? mascaraPrecoCentavos(String(cupom.descontoMaximoCentavos))
            : "",
        usoMaximoTotal: cupom.usoMaximoTotal != null ? String(cupom.usoMaximoTotal) : "",
        usoMaximoPorUsuario: String(cupom.usoMaximoPorUsuario ?? 1),
        vigenciaInicio: paraInputDate(cupom.vigenciaInicio),
        vigenciaFim: paraInputDate(cupom.vigenciaFim),
        primeiraCompraApenas: !!cupom.primeiraCompraApenas,
        idCategoriaRestrita: cupom.idCategoriaRestrita ?? "",
        idColecaoRestrita: cupom.idColecaoRestrita ?? "",
        ativo: !!cupom.ativo,
    };
}

/** Só a data crua vem da API; quem decide se está vigente é quem exibe. */
function estaVigente(cupom) {
    const agora = Date.now();
    const inicio = cupom.vigenciaInicio ? new Date(cupom.vigenciaInicio).getTime() : 0;
    const fim = cupom.vigenciaFim ? new Date(cupom.vigenciaFim).getTime() : Infinity;
    return cupom.ativo && !cupom.esgotado && agora >= inicio && agora <= fim;
}

function descreverValor(cupom) {
    if (cupom.tipo === TIPO_CUPOM_FRETE_GRATIS) return "Frete grátis";
    if (cupom.tipo === TIPO_CUPOM_PERCENTUAL) {
        return `${(cupom.valor / 100).toLocaleString("pt-BR")}%`;
    }
    return formatarCentavosParaBRL(cupom.valor);
}

export default function Cupons() {
    const [buscaDigitada, setBuscaDigitada] = useState("");
    const [busca, setBusca] = useState("");
    const [ativo, setAtivo] = useState("");
    const [pagina, setPagina] = useState(1);

    const [formulario, setFormulario] = useState(null);
    const [erros, setErros] = useState({});
    const [confirmarExclusao, setConfirmarExclusao] = useState(null);
    const [verUsos, setVerUsos] = useState(null);

    useEffect(() => {
        const t = setTimeout(() => setBusca(buscaDigitada.trim()), 400);
        return () => clearTimeout(t);
    }, [buscaDigitada]);

    useEffect(() => {
        setPagina(1);
    }, [busca, ativo]);

    const filtros = useMemo(
        () => ({
            search: busca || undefined,
            ativo: ativo === "" ? undefined : ativo === "ativos",
            page: pagina,
            pageSize: ITENS_POR_PAGINA,
        }),
        [busca, ativo, pagina],
    );

    const { cupons, total, totalPaginas, tamanhoPagina, isLoading, isError, refetch } =
        useCupons(filtros);

    const { criar, atualizar, remover } = useAcoesCupom();
    const usos = useUsosDoCupom(verUsos?.id, { page: 1, pageSize: 20 }, { habilitado: !!verUsos });

    const abrirNovo = () => {
        setErros({});
        setFormulario({ ...FORMULARIO_VAZIO, vigenciaInicio: paraInputDate(new Date()) });
    };

    const validar = (f) => {
        const e = {};
        if (!/^[A-Za-z0-9._-]{3,40}$/.test(f.codigo.trim())) {
            e.codigo = "De 3 a 40 caracteres: letras, números, ponto, hífen ou sublinhado.";
        }
        if (!f.vigenciaInicio) e.vigenciaInicio = "Informe quando o cupom passa a valer.";
        if (f.vigenciaFim && f.vigenciaFim < f.vigenciaInicio) {
            e.vigenciaFim = "O fim não pode ser antes do início.";
        }
        if (f.tipo === TIPO_CUPOM_PERCENTUAL) {
            const pct = Number(String(f.percentual).replace(",", "."));
            if (!(pct > 0 && pct <= 100)) e.percentual = "Informe um percentual entre 0 e 100.";
        }
        if (f.tipo === TIPO_CUPOM_VALOR_FIXO && parseBRLParaCentavos(f.valorFixo) <= 0) {
            e.valorFixo = "Informe o valor do desconto.";
        }
        if (Number(f.usoMaximoPorUsuario) < 1) {
            e.usoMaximoPorUsuario = "Cada pessoa precisa poder usar ao menos uma vez.";
        }
        return e;
    };

    const salvar = () => {
        const e = validar(formulario);
        setErros(e);
        if (Object.keys(e).length > 0) return;

        const valor =
            formulario.tipo === TIPO_CUPOM_PERCENTUAL
                ? Math.round(Number(String(formulario.percentual).replace(",", ".")) * 100)
                : formulario.tipo === TIPO_CUPOM_VALOR_FIXO
                  ? parseBRLParaCentavos(formulario.valorFixo)
                  : 0;

        const corpo = {
            codigo: formulario.codigo,
            descricao: formulario.descricao,
            tipo: formulario.tipo,
            valor,
            valorMinimoPedidoCentavos: parseBRLParaCentavos(formulario.valorMinimo) || null,
            descontoMaximoCentavos: parseBRLParaCentavos(formulario.descontoMaximo) || null,
            usoMaximoTotal: formulario.usoMaximoTotal
                ? Number(formulario.usoMaximoTotal)
                : null,
            usoMaximoPorUsuario: Number(formulario.usoMaximoPorUsuario) || 1,
            vigenciaInicio: paraParametroUtc(inicioDoDiaLocal(formulario.vigenciaInicio)),
            vigenciaFim: formulario.vigenciaFim
                ? paraParametroUtc(fimDoDiaLocal(formulario.vigenciaFim))
                : null,
            primeiraCompraApenas: formulario.primeiraCompraApenas,
            idCategoriaRestrita: formulario.idCategoriaRestrita
                ? Number(formulario.idCategoriaRestrita)
                : null,
            idColecaoRestrita: formulario.idColecaoRestrita
                ? Number(formulario.idColecaoRestrita)
                : null,
            ativo: formulario.ativo,
        };

        const acao = formulario.id
            ? atualizar.mutate({ id: formulario.id, ...corpo }, { onSuccess: () => setFormulario(null) })
            : criar.mutate(corpo, { onSuccess: () => setFormulario(null) });

        return acao;
    };

    const colunas = [
        {
            chave: "cupomCodigo",
            titulo: "Código",
            render: (c) => (
                <div className="min-w-0">
                    <p className="preco text-sm text-ink">{c.codigo}</p>
                    {c.descricao && (
                        <p className="truncate text-xs text-ink-soft">{c.descricao}</p>
                    )}
                </div>
            ),
        },
        {
            chave: "cupomTipo",
            titulo: "Tipo",
            render: (c) => <BadgeStatus mapa={TIPO_CUPOM} valor={c.tipo} />,
        },
        {
            chave: "cupomValor",
            titulo: "Desconto",
            alinhamento: "direita",
            render: (c) => (
                <div>
                    <p className="preco text-sm text-ink">{descreverValor(c)}</p>
                    {c.descontoMaximoCentavos && (
                        <p className="preco text-xs text-ink-soft">
                            teto {formatarCentavosParaBRL(c.descontoMaximoCentavos)}
                        </p>
                    )}
                </div>
            ),
        },
        {
            chave: "cupomVigencia",
            titulo: "Vigência",
            render: (c) => (
                <span className="text-xs text-ink-soft">
                    {formatarData(c.vigenciaInicio)} —{" "}
                    {c.vigenciaFim ? formatarData(c.vigenciaFim) : "sem fim"}
                </span>
            ),
        },
        {
            chave: "cupomUsos",
            titulo: "Usos",
            alinhamento: "direita",
            render: (c) => (
                <span className="preco text-sm text-ink">
                    {c.usosAtuais}
                    {c.usoMaximoTotal ? ` / ${c.usoMaximoTotal}` : ""}
                </span>
            ),
        },
        {
            chave: "cupomSituacao",
            titulo: "Situação",
            render: (c) =>
                c.esgotado ? (
                    <Badge variante="esgotado">Esgotado</Badge>
                ) : !c.ativo ? (
                    <Badge variante="neutro">Inativo</Badge>
                ) : estaVigente(c) ? (
                    <Badge variante="sucesso">Vigente</Badge>
                ) : (
                    <Badge variante="alerta">Fora da vigência</Badge>
                ),
        },
        {
            chave: "cupomAcoes",
            titulo: "",
            alinhamento: "direita",
            render: (c) => (
                <div className="flex justify-end gap-1">
                    <Botao variante="texto" tamanho="sm" onClick={() => setVerUsos(c)}>
                        <FiBarChart2 size={13} aria-hidden="true" />
                        <span className="sr-only">Ver usos do cupom {c.codigo}</span>
                    </Botao>
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() => {
                            setErros({});
                            setFormulario(paraFormulario(c));
                        }}
                    >
                        Editar
                    </Botao>
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() => setConfirmarExclusao(c)}
                        className="text-danger hover:text-danger"
                    >
                        Excluir
                    </Botao>
                </div>
            ),
        },
    ];

    const ehPercentual = formulario?.tipo === TIPO_CUPOM_PERCENTUAL;
    const ehValorFixo = formulario?.tipo === TIPO_CUPOM_VALOR_FIXO;

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Operação"
                titulo="Cupons"
                descricao="Promoção é investimento: o contador de usos e o relatório de cada cupom mostram quanto a campanha realmente custou."
                acoes={
                    <Botao tamanho="sm" onClick={abrirNovo}>
                        <FiPlus size={14} aria-hidden="true" /> Novo cupom
                    </Botao>
                }
            />

            <div className="mb-6 flex flex-wrap items-end gap-4 border border-sand bg-linen p-4">
                <Campo
                    label="Buscar"
                    placeholder="Código ou descrição"
                    value={buscaDigitada}
                    onChange={(e) => setBuscaDigitada(e.target.value)}
                    containerClassName="min-w-[16rem] flex-1"
                />
                <Campo
                    label="Situação"
                    como="select"
                    value={ativo}
                    onChange={(e) => setAtivo(e.target.value)}
                    containerClassName="w-48"
                >
                    <option value="">Ativos e inativos</option>
                    <option value="ativos">Somente ativos</option>
                    <option value="inativos">Somente inativos</option>
                </Campo>
            </div>

            {isError ? (
                <EstadoErro mensagem="A lista de cupons não pôde ser carregada." onTentarDeNovo={refetch} />
            ) : !isLoading && cupons.length === 0 ? (
                <EstadoVazio
                    Icone={FiSearch}
                    titulo={busca || ativo ? "Nenhum cupom com esses filtros" : "Nenhum cupom criado"}
                    mensagem={
                        busca || ativo
                            ? "Limpe a busca ou troque a situação para ver a lista inteira."
                            : "Crie o primeiro cupom para começar uma campanha. Você define o teto do desconto, o pedido mínimo e quantas vezes cada pessoa pode usar."
                    }
                    acao={
                        <Botao tamanho="sm" onClick={abrirNovo}>
                            Criar cupom
                        </Botao>
                    }
                />
            ) : (
                <>
                    <Tabela
                        colunas={colunas}
                        dados={cupons}
                        carregando={isLoading}
                        chaveLinha={(c) => c.id}
                        vazio="Nenhum cupom nesta página."
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

            {/* ------------------------------------------------- formulário */}
            <Modal
                isOpen={!!formulario}
                onClose={() => setFormulario(null)}
                titulo={formulario?.id ? "Editar cupom" : "Novo cupom"}
                largura="xl"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setFormulario(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            onClick={salvar}
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                {formulario && (
                    <div className="grid gap-4 sm:grid-cols-2">
                        <Campo
                            label="Código"
                            obrigatorio
                            maxLength={40}
                            value={formulario.codigo}
                            erro={erros.codigo}
                            ajuda="É o que a pessoa digita no carrinho. Vira maiúsculas ao salvar."
                            onChange={(e) =>
                                setFormulario({ ...formulario, codigo: e.target.value })
                            }
                        />

                        <Campo
                            label="Descrição interna"
                            maxLength={200}
                            value={formulario.descricao}
                            ajuda="Só a equipe vê. Diga a que campanha pertence."
                            onChange={(e) =>
                                setFormulario({ ...formulario, descricao: e.target.value })
                            }
                        />

                        <Campo
                            label="Tipo"
                            como="select"
                            value={formulario.tipo}
                            onChange={(e) =>
                                setFormulario({ ...formulario, tipo: Number(e.target.value) })
                            }
                        >
                            {TIPO_CUPOM.map((t) => (
                                <option key={t.valor} value={t.valor}>
                                    {t.rotulo}
                                </option>
                            ))}
                        </Campo>

                        {ehPercentual && (
                            <Campo
                                label="Percentual de desconto"
                                type="number"
                                min="0"
                                max="100"
                                step="0.01"
                                obrigatorio
                                value={formulario.percentual}
                                erro={erros.percentual}
                                ajuda="Em por cento. Aceita casas decimais."
                                onChange={(e) =>
                                    setFormulario({ ...formulario, percentual: e.target.value })
                                }
                            />
                        )}

                        {ehValorFixo && (
                            <Campo
                                label="Valor do desconto"
                                inputMode="numeric"
                                obrigatorio
                                value={formulario.valorFixo}
                                erro={erros.valorFixo}
                                ajuda="Em reais. Digite só os números."
                                onChange={(e) =>
                                    setFormulario({
                                        ...formulario,
                                        valorFixo: mascaraPrecoCentavos(e.target.value),
                                    })
                                }
                            />
                        )}

                        {formulario.tipo === TIPO_CUPOM_FRETE_GRATIS && (
                            <p className="self-end text-xs text-ink-soft">
                                Frete grátis zera o frete do pedido. O campo de valor não se aplica.
                            </p>
                        )}

                        <Campo
                            label="Pedido mínimo"
                            inputMode="numeric"
                            value={formulario.valorMinimo}
                            ajuda="Deixe em branco para valer em qualquer valor."
                            onChange={(e) =>
                                setFormulario({
                                    ...formulario,
                                    valorMinimo: mascaraPrecoCentavos(e.target.value),
                                })
                            }
                        />

                        <Campo
                            label="Teto do desconto"
                            inputMode="numeric"
                            value={formulario.descontoMaximo}
                            ajuda="É o que evita um percentual alto virar prejuízo em pedido grande."
                            onChange={(e) =>
                                setFormulario({
                                    ...formulario,
                                    descontoMaximo: mascaraPrecoCentavos(e.target.value),
                                })
                            }
                        />

                        <Campo
                            label="Início da vigência"
                            type="date"
                            obrigatorio
                            value={formulario.vigenciaInicio}
                            erro={erros.vigenciaInicio}
                            onChange={(e) =>
                                setFormulario({ ...formulario, vigenciaInicio: e.target.value })
                            }
                        />

                        <Campo
                            label="Fim da vigência"
                            type="date"
                            value={formulario.vigenciaFim}
                            erro={erros.vigenciaFim}
                            ajuda="Em branco: sem data de encerramento."
                            onChange={(e) =>
                                setFormulario({ ...formulario, vigenciaFim: e.target.value })
                            }
                        />

                        <Campo
                            label="Limite total de usos"
                            type="number"
                            min="1"
                            value={formulario.usoMaximoTotal}
                            ajuda="Em branco: ilimitado."
                            onChange={(e) =>
                                setFormulario({ ...formulario, usoMaximoTotal: e.target.value })
                            }
                        />

                        <Campo
                            label="Usos por pessoa"
                            type="number"
                            min="1"
                            obrigatorio
                            value={formulario.usoMaximoPorUsuario}
                            erro={erros.usoMaximoPorUsuario}
                            onChange={(e) =>
                                setFormulario({
                                    ...formulario,
                                    usoMaximoPorUsuario: e.target.value,
                                })
                            }
                        />

                        <Campo
                            label="Restringir à categoria"
                            type="number"
                            min="1"
                            value={formulario.idCategoriaRestrita}
                            ajuda="Identificador da categoria. Em branco vale para o catálogo todo."
                            onChange={(e) =>
                                setFormulario({
                                    ...formulario,
                                    idCategoriaRestrita: e.target.value,
                                })
                            }
                        />

                        <Campo
                            label="Restringir à coleção"
                            type="number"
                            min="1"
                            value={formulario.idColecaoRestrita}
                            ajuda="Identificador da coleção. Em branco vale para o catálogo todo."
                            onChange={(e) =>
                                setFormulario({ ...formulario, idColecaoRestrita: e.target.value })
                            }
                        />

                        <label className="flex items-center gap-3 text-sm text-ink">
                            <input
                                type="checkbox"
                                className="h-4 w-4 accent-olive"
                                checked={formulario.primeiraCompraApenas}
                                onChange={(e) =>
                                    setFormulario({
                                        ...formulario,
                                        primeiraCompraApenas: e.target.checked,
                                    })
                                }
                            />
                            Somente na primeira compra
                        </label>

                        <label className="flex items-center gap-3 text-sm text-ink">
                            <input
                                type="checkbox"
                                className="h-4 w-4 accent-olive"
                                checked={formulario.ativo}
                                onChange={(e) =>
                                    setFormulario({ ...formulario, ativo: e.target.checked })
                                }
                            />
                            Cupom ativo
                        </label>
                    </div>
                )}
            </Modal>

            {/* ------------------------------------------------------- usos */}
            <Modal
                isOpen={!!verUsos}
                onClose={() => setVerUsos(null)}
                titulo={verUsos ? `Usos de ${verUsos.codigo}` : "Usos"}
                largura="lg"
                rodape={
                    <Botao variante="contorno" onClick={() => setVerUsos(null)}>
                        Fechar
                    </Botao>
                }
            >
                {usos.isLoading ? (
                    <p className="py-6 text-sm text-ink-soft">Carregando…</p>
                ) : usos.usos.length === 0 ? (
                    <p className="py-6 text-sm text-ink-soft">
                        Este cupom ainda não foi usado em nenhum pedido concluído.
                    </p>
                ) : (
                    <ul className="divide-y divide-sand/60">
                        {usos.usos.map((u) => (
                            <li key={u.id} className="flex items-start justify-between gap-4 py-3">
                                <div className="min-w-0">
                                    <p className="text-sm text-ink">
                                        {u.nomeUsuario || u.emailUsuario || "Cliente"}
                                    </p>
                                    <p className="preco text-xs text-ink-soft">
                                        {u.numeroPedido || u.idPedido} ·{" "}
                                        {formatarDataHora(u.dataUso)}
                                    </p>
                                </div>
                                <p className="preco shrink-0 text-sm text-clay">
                                    − {formatarCentavosParaBRL(u.valorDescontadoCentavos)}
                                </p>
                            </li>
                        ))}
                    </ul>
                )}
                <p className="mt-4 text-xs text-taupe">
                    O valor de cada linha é o que saiu no dia do uso, não um recálculo com as regras
                    de hoje.
                </p>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmarExclusao}
                titulo="Excluir cupom"
                mensagem={
                    confirmarExclusao
                        ? `O cupom ${confirmarExclusao.codigo} deixa de existir. O histórico de quem já usou permanece nos pedidos.`
                        : ""
                }
                carregando={remover.isPending}
                onCancel={() => setConfirmarExclusao(null)}
                onConfirm={() =>
                    remover.mutate(confirmarExclusao.id, {
                        onSuccess: () => setConfirmarExclusao(null),
                    })
                }
            />
        </div>
    );
}
