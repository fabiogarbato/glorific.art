import { useMemo, useState } from "react";
import { FiCheck, FiGrid, FiRotateCcw, FiSlash } from "react-icons/fi";
import Botao from "@/components/ui/Botao.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import CampoDinheiro from "./CampoDinheiro.jsx";
import SeletorCor, { Swatch } from "./SeletorCor.jsx";
import Aviso from "./Aviso.jsx";
import EstadoVazio from "./EstadoVazio.jsx";
import { LIMITES, LIMITES_LOGISTICA } from "@/lib/dominioCatalogo.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Matriz de variações: escolher tamanhos e cores, gerar a grade em lote e
 * ajustar SKU, preço, peso e dimensões linha a linha.
 *
 * Duas regras do backend moldam esta tela:
 *
 * 1. Peso e dimensões são obrigatórios e POSITIVOS já na criação do SKU — o
 *    banco tem CHECK (peso_gramas > 0 AND altura_cm > 0 ...). Sem eles não há
 *    cálculo de frete e a peça não pode ser vendida. Por isso o salvamento é
 *    bloqueado aqui, com mensagem por campo, em vez de deixar o erro de
 *    constraint chegar cru na tela.
 *
 * 2. Gerar a grade de novo NÃO sobrescreve o que já existe: as combinações
 *    presentes são preservadas com o preço e o peso já ajustados à mão. Dá para
 *    acrescentar uma cor depois e completar a grade sem perder o trabalho.
 */

/** Converte "12,5" ou "12.5" em número. Vazio vira null (campo não preenchido). */
function paraDecimal(texto) {
    if (texto === null || texto === undefined || String(texto).trim() === "") return null;
    const numero = Number(String(texto).replace(",", "."));
    return Number.isFinite(numero) ? numero : NaN;
}

function paraInteiro(texto) {
    if (texto === null || texto === undefined || String(texto).trim() === "") return null;
    const numero = Number(String(texto).replace(/\D/g, ""));
    return Number.isFinite(numero) && String(texto).trim() !== "" ? numero : NaN;
}

const CAMPOS_DIMENSAO = [
    {
        campo: "alturaCm",
        rotulo: "Altura (cm)",
        faltando: "Informe a altura. Sem ela não há cálculo de frete.",
        invalido: "A altura em cm deve ser maior que zero.",
        fora: "A altura está fora da faixa aceita.",
    },
    {
        campo: "larguraCm",
        rotulo: "Largura (cm)",
        faltando: "Informe a largura. Sem ela não há cálculo de frete.",
        invalido: "A largura em cm deve ser maior que zero.",
        fora: "A largura está fora da faixa aceita.",
    },
    {
        campo: "comprimentoCm",
        rotulo: "Comprimento (cm)",
        faltando: "Informe o comprimento. Sem ele não há cálculo de frete.",
        invalido: "O comprimento em cm deve ser maior que zero.",
        fora: "O comprimento está fora da faixa aceita.",
    },
];

/**
 * Valida peso e dimensões contra os `[Range]` do `ProdutoVariacaoCreateDto`.
 * Devolve `{ campo: mensagem }` — vazio significa "pode salvar".
 */
function validarLogistica(valores) {
    const erros = {};
    const { pesoMinimo, pesoMaximo, dimensaoMinima, dimensaoMaxima } = LIMITES_LOGISTICA;

    const peso = paraInteiro(valores.pesoGramas);
    if (peso === null) erros.pesoGramas = "Informe o peso. Sem ele não há cálculo de frete.";
    else if (Number.isNaN(peso) || peso < pesoMinimo)
        erros.pesoGramas = "O peso em gramas deve ser maior que zero.";
    else if (peso > pesoMaximo) erros.pesoGramas = `O peso deve ser de até ${pesoMaximo} g.`;

    for (const { campo, faltando, invalido, fora } of CAMPOS_DIMENSAO) {
        const valor = paraDecimal(valores[campo]);

        if (valor === null) erros[campo] = faltando;
        else if (Number.isNaN(valor) || valor < dimensaoMinima) erros[campo] = invalido;
        else if (valor > dimensaoMaxima) erros[campo] = fora;
    }

    return erros;
}

/** Converte o rascunho da tela nos tipos que o DTO espera. */
function logisticaParaPayload(valores) {
    return {
        pesoGramas: paraInteiro(valores.pesoGramas),
        alturaCm: paraDecimal(valores.alturaCm),
        larguraCm: paraDecimal(valores.larguraCm),
        comprimentoCm: paraDecimal(valores.comprimentoCm),
    };
}

const LOGISTICA_VAZIA = { pesoGramas: "", alturaCm: "", larguraCm: "", comprimentoCm: "" };

const GERADOR_INICIAL = {
    ...LOGISTICA_VAZIA,
    precoCentavos: null,
    prefixoSku: "",
    quantidadeInicial: "0",
    quantidadeMinima: "0",
    ativo: true,
};

function rascunhoDaVariacao(variacao) {
    return {
        sku: variacao.sku ?? "",
        precoCentavos: variacao.precoCentavos ?? null,
        codigoBarras: variacao.codigoBarras ?? "",
        pesoGramas: String(variacao.pesoGramas ?? ""),
        alturaCm: String(variacao.alturaCm ?? ""),
        larguraCm: String(variacao.larguraCm ?? ""),
        comprimentoCm: String(variacao.comprimentoCm ?? ""),
        ativo: !!variacao.ativo,
    };
}

// ---------------------------------------------------------------------------

export default function MatrizVariacoes({
    variacoes = [],
    tamanhos = [],
    cores = [],
    precoBaseCentavos = 0,
    skuBase = "",
    carregando = false,
    incluirInativas = false,
    onIncluirInativas,
    onGerarGrade,
    gerando = false,
    onSalvarLinha,
    salvandoId = null,
    onDesativar,
    onAtivar,
}) {
    const [gerador, setGerador] = useState(GERADOR_INICIAL);
    const [errosGerador, setErrosGerador] = useState({});
    const [idsTamanhos, setIdsTamanhos] = useState([]);
    const [idsCores, setIdsCores] = useState([]);

    const [rascunhos, setRascunhos] = useState({});
    const [errosLinha, setErrosLinha] = useState({});

    const combinacoesExistentes = useMemo(
        () => new Set(variacoes.map((v) => `${v.idTamanho}:${v.idCor}`)),
        [variacoes],
    );

    const novasCombinacoes = useMemo(
        () =>
            idsTamanhos.reduce(
                (total, idTamanho) =>
                    total +
                    idsCores.filter((idCor) => !combinacoesExistentes.has(`${idTamanho}:${idCor}`))
                        .length,
                0,
            ),
        [idsTamanhos, idsCores, combinacoesExistentes],
    );

    const semLogistica = useMemo(
        () =>
            variacoes.filter(
                (v) =>
                    v.ativo &&
                    (!v.pesoGramas || !v.alturaCm || !v.larguraCm || !v.comprimentoCm),
            ),
        [variacoes],
    );

    const alternar = (lista, definir) => (id) =>
        definir(lista.includes(id) ? lista.filter((x) => x !== id) : [...lista, id]);

    // ------------------------------------------------------------ Gerador

    const submeterGrade = (evento) => {
        evento.preventDefault();

        const erros = validarLogistica(gerador);
        if (idsTamanhos.length === 0) erros.tamanhos = "Escolha ao menos um tamanho.";
        if (idsCores.length === 0) erros.cores = "Escolha ao menos uma cor.";

        setErrosGerador(erros);
        if (Object.keys(erros).length > 0) return;

        onGerarGrade?.({
            idsTamanhos,
            idsCores,
            ...logisticaParaPayload(gerador),
            precoCentavos: gerador.precoCentavos,
            prefixoSku: gerador.prefixoSku.trim() || null,
            ativo: gerador.ativo,
            quantidadeInicial: paraInteiro(gerador.quantidadeInicial) ?? 0,
            quantidadeMinima: paraInteiro(gerador.quantidadeMinima) ?? 0,
        });
    };

    // -------------------------------------------------------------- Linhas

    const rascunhoDe = (variacao) => rascunhos[variacao.id] ?? rascunhoDaVariacao(variacao);

    const editar = (variacao, campo, valor) =>
        setRascunhos((atual) => ({
            ...atual,
            [variacao.id]: { ...rascunhoDe(variacao), [campo]: valor },
        }));

    const sujo = (variacao) => {
        const rascunho = rascunhos[variacao.id];
        if (!rascunho) return false;
        const original = rascunhoDaVariacao(variacao);
        return Object.keys(original).some((chave) => rascunho[chave] !== original[chave]);
    };

    const salvarLinha = (variacao) => {
        const rascunho = rascunhoDe(variacao);
        const erros = validarLogistica(rascunho);

        setErrosLinha((atual) => ({ ...atual, [variacao.id]: erros }));
        if (Object.keys(erros).length > 0) return;

        onSalvarLinha?.(variacao.id, {
            sku: rascunho.sku.trim() || null,
            precoCentavos: rascunho.precoCentavos,
            codigoBarras: rascunho.codigoBarras.trim() || null,
            ...logisticaParaPayload(rascunho),
            ativo: rascunho.ativo,
        });

        setRascunhos((atual) => {
            const copia = { ...atual };
            delete copia[variacao.id];
            return copia;
        });
    };

    const erroDe = (variacao, campo) => errosLinha[variacao.id]?.[campo];

    // ----------------------------------------------------------- Render

    const campoLogistica = (valores, erros, aoMudar, prefixoId) => (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Campo
                id={`${prefixoId}-peso`}
                label="Peso (g)"
                obrigatorio
                inputMode="numeric"
                value={valores.pesoGramas}
                erro={erros.pesoGramas}
                onChange={(e) => aoMudar("pesoGramas", e.target.value.replace(/\D/g, ""))}
            />
            {CAMPOS_DIMENSAO.map(({ campo, rotulo }) => (
                <Campo
                    key={campo}
                    id={`${prefixoId}-${campo}`}
                    label={rotulo}
                    obrigatorio
                    inputMode="decimal"
                    value={valores[campo]}
                    erro={erros[campo]}
                    onChange={(e) => aoMudar(campo, e.target.value.replace(/[^\d.,]/g, ""))}
                />
            ))}
        </div>
    );

    return (
        <div className="flex flex-col gap-8">
            {/* ------------------------------------------------ Aviso de frete */}
            <Aviso variante="alerta" titulo="Peso e dimensões são obrigatórios">
                <p>
                    A transportadora cobra por peso e por volume. Uma variação sem peso, altura,
                    largura e comprimento não entra no cálculo de frete e a peça não pode ser
                    vendida. Estes campos são recusados pela API quando ficam em zero.
                </p>
                {semLogistica.length > 0 && (
                    <p className="mt-2 font-sans text-sm text-danger">
                        {semLogistica.length === 1
                            ? "1 variação ativa está sem peso ou dimensões."
                            : `${semLogistica.length} variações ativas estão sem peso ou dimensões.`}{" "}
                        Complete os campos abaixo antes de publicar a peça.
                    </p>
                )}
            </Aviso>

            {/* ------------------------------------------------------ Gerador */}
            <section className="border border-sand bg-linen/50 p-4 sm:p-6">
                <div className="mb-5 flex items-start gap-3">
                    <FiGrid size={18} className="mt-1 shrink-0 text-ink-soft" aria-hidden="true" />
                    <div>
                        <h2 className="font-display text-xl tracking-tight text-ink">
                            Gerar grade em lote
                        </h2>
                        <p className="mt-1 text-sm leading-relaxed text-ink-soft">
                            Escolha os tamanhos e as cores: a matriz vira um SKU por combinação. As
                            combinações que já existem são preservadas como estão, com o preço e o
                            peso que você ajustou à mão.
                        </p>
                    </div>
                </div>

                <form onSubmit={submeterGrade} className="flex flex-col gap-6">
                    <fieldset className="flex flex-col gap-2">
                        <legend className="eyebrow mb-1">
                            Tamanhos<span className="ml-1 text-danger">*</span>
                        </legend>

                        {tamanhos.length === 0 ? (
                            <p className="text-sm text-ink-soft">
                                Nenhum tamanho ativo cadastrado. Cadastre a grade de tamanhos antes
                                de montar as variações.
                            </p>
                        ) : (
                            <div className="flex flex-wrap gap-2">
                                {tamanhos.map((tamanho) => {
                                    const marcado = idsTamanhos.includes(tamanho.id);
                                    return (
                                        <button
                                            key={tamanho.id}
                                            type="button"
                                            aria-pressed={marcado}
                                            onClick={() =>
                                                alternar(idsTamanhos, setIdsTamanhos)(tamanho.id)
                                            }
                                            className={`h-11 min-w-[3rem] border px-3 font-sans text-xs uppercase tracking-widest transition-colors ${
                                                marcado
                                                    ? "border-olive bg-olive text-bone"
                                                    : "border-sand bg-base-100 text-ink-soft hover:border-ink"
                                            }`}
                                        >
                                            {tamanho.codigo}
                                        </button>
                                    );
                                })}
                            </div>
                        )}

                        {errosGerador.tamanhos && (
                            <p role="alert" className="text-xs text-danger">
                                {errosGerador.tamanhos}
                            </p>
                        )}
                    </fieldset>

                    <SeletorCor
                        label="Cores"
                        obrigatorio
                        multiplo
                        cores={cores}
                        valores={idsCores}
                        onAlternar={alternar(idsCores, setIdsCores)}
                        erro={errosGerador.cores}
                    />

                    {campoLogistica(
                        gerador,
                        errosGerador,
                        (campo, valor) => setGerador((a) => ({ ...a, [campo]: valor })),
                        "grade",
                    )}

                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
                        <CampoDinheiro
                            label="Preço da variação"
                            valorCentavos={gerador.precoCentavos}
                            onChange={(v) => setGerador((a) => ({ ...a, precoCentavos: v }))}
                            ajuda={`Em branco, herda o preço base: ${formatarCentavosParaBRL(precoBaseCentavos)}.`}
                        />
                        <Campo
                            label="Prefixo do SKU"
                            value={gerador.prefixoSku}
                            maxLength={30}
                            placeholder={skuBase || "Herda o SKU base"}
                            ajuda="Em branco, usa o SKU base da peça."
                            onChange={(e) =>
                                setGerador((a) => ({ ...a, prefixoSku: e.target.value }))
                            }
                        />
                        <Campo
                            label="Estoque inicial"
                            inputMode="numeric"
                            value={gerador.quantidadeInicial}
                            ajuda="Saldo de prateleira que cada SKU novo já nasce tendo."
                            onChange={(e) =>
                                setGerador((a) => ({
                                    ...a,
                                    quantidadeInicial: e.target.value.replace(/\D/g, ""),
                                }))
                            }
                        />
                        <Campo
                            label="Estoque mínimo"
                            inputMode="numeric"
                            value={gerador.quantidadeMinima}
                            ajuda="Limite do alerta de reposição no painel."
                            onChange={(e) =>
                                setGerador((a) => ({
                                    ...a,
                                    quantidadeMinima: e.target.value.replace(/\D/g, ""),
                                }))
                            }
                        />
                    </div>

                    <div className="flex flex-wrap items-center justify-between gap-4 border-t border-sand pt-4">
                        <label className="flex items-center gap-2 font-sans text-sm text-ink">
                            <input
                                type="checkbox"
                                checked={gerador.ativo}
                                onChange={(e) =>
                                    setGerador((a) => ({ ...a, ativo: e.target.checked }))
                                }
                                className="h-4 w-4 accent-olive"
                            />
                            Já criar as variações ativas
                        </label>

                        <div className="flex items-center gap-4">
                            <p className="text-xs text-ink-soft">
                                {novasCombinacoes === 0
                                    ? "Nenhuma combinação nova selecionada."
                                    : novasCombinacoes === 1
                                      ? "1 variação nova será criada."
                                      : `${novasCombinacoes} variações novas serão criadas.`}
                            </p>
                            <Botao type="submit" carregando={gerando} disabled={gerando}>
                                Gerar grade
                            </Botao>
                        </div>
                    </div>
                </form>
            </section>

            {/* -------------------------------------------------------- Grade */}
            <section>
                <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                    <h2 className="font-display text-xl tracking-tight text-ink">
                        Grade da peça
                        {variacoes.length > 0 && (
                            <span className="ml-2 font-sans text-sm text-ink-soft">
                                {variacoes.length}
                            </span>
                        )}
                    </h2>

                    <label className="flex items-center gap-2 font-sans text-sm text-ink-soft">
                        <input
                            type="checkbox"
                            checked={incluirInativas}
                            onChange={(e) => onIncluirInativas?.(e.target.checked)}
                            className="h-4 w-4 accent-olive"
                        />
                        Mostrar variações desativadas
                    </label>
                </div>

                {carregando && (
                    <div className="flex flex-col gap-3">
                        {Array.from({ length: 4 }).map((_, i) => (
                            <Skeleton key={`sk-${i}`} className="h-24 w-full" />
                        ))}
                    </div>
                )}

                {!carregando && variacoes.length === 0 && (
                    <EstadoVazio
                        titulo="A peça ainda não tem SKU"
                        mensagem="Escolha os tamanhos e as cores acima e gere a grade. Cada combinação vira um SKU vendável, com preço, peso e dimensões próprios."
                    />
                )}

                {!carregando &&
                    variacoes.map((variacao) => {
                        const rascunho = rascunhoDe(variacao);
                        const alterada = sujo(variacao);
                        const salvando = salvandoId === variacao.id;

                        return (
                            <article
                                key={variacao.id}
                                className={`mb-3 border bg-base-100 p-4 sm:p-5 ${
                                    variacao.ativo ? "border-sand" : "border-sand bg-linen/40"
                                }`}
                            >
                                <header className="mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-sand pb-3">
                                    <div className="flex min-w-0 items-center gap-3">
                                        <Swatch
                                            cor={{
                                                hexRgb: variacao.hexRgb,
                                                nome: variacao.nomeCor,
                                            }}
                                            tamanho={22}
                                        />
                                        <div className="min-w-0">
                                            <p className="font-display text-lg leading-tight text-ink">
                                                {variacao.codigoTamanho} · {variacao.nomeCor}
                                            </p>
                                            <p className="preco truncate font-sans text-xs text-ink-soft">
                                                {variacao.sku}
                                            </p>
                                        </div>
                                    </div>

                                    <div className="flex flex-wrap items-center gap-2">
                                        <Badge variante={variacao.ativo ? "neutro" : "esgotado"}>
                                            {variacao.ativo ? "Ativa" : "Desativada"}
                                        </Badge>
                                        <span className="preco font-sans text-xs text-ink-soft">
                                            Disponível: {variacao.quantidadeDisponivel}
                                            {variacao.quantidadeReservada > 0 &&
                                                ` · reservado: ${variacao.quantidadeReservada}`}
                                        </span>
                                    </div>
                                </header>

                                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                                    <Campo
                                        label="SKU"
                                        value={rascunho.sku}
                                        maxLength={LIMITES.variacaoSku}
                                        ajuda="Em branco, mantém o SKU atual."
                                        onChange={(e) => editar(variacao, "sku", e.target.value)}
                                    />
                                    <CampoDinheiro
                                        label="Preço da variação"
                                        valorCentavos={rascunho.precoCentavos}
                                        onChange={(v) => editar(variacao, "precoCentavos", v)}
                                        ajuda={`Em branco, vale o preço base: ${formatarCentavosParaBRL(precoBaseCentavos)}. Hoje vende por ${formatarCentavosParaBRL(variacao.precoEfetivoCentavos)}.`}
                                    />
                                    <Campo
                                        label="Código de barras"
                                        value={rascunho.codigoBarras}
                                        maxLength={LIMITES.codigoBarras}
                                        onChange={(e) =>
                                            editar(variacao, "codigoBarras", e.target.value)
                                        }
                                    />
                                </div>

                                <div className="mt-4">
                                    {campoLogistica(
                                        rascunho,
                                        {
                                            pesoGramas: erroDe(variacao, "pesoGramas"),
                                            alturaCm: erroDe(variacao, "alturaCm"),
                                            larguraCm: erroDe(variacao, "larguraCm"),
                                            comprimentoCm: erroDe(variacao, "comprimentoCm"),
                                        },
                                        (campo, valor) => editar(variacao, campo, valor),
                                        `variacao-${variacao.id}`,
                                    )}
                                </div>

                                <footer className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-sand pt-4">
                                    <label className="flex items-center gap-2 font-sans text-sm text-ink">
                                        <input
                                            type="checkbox"
                                            checked={rascunho.ativo}
                                            onChange={(e) =>
                                                editar(variacao, "ativo", e.target.checked)
                                            }
                                            className="h-4 w-4 accent-olive"
                                        />
                                        Variação ativa
                                    </label>

                                    <div className="flex flex-wrap items-center gap-2">
                                        {variacao.ativo ? (
                                            <Botao
                                                variante="texto"
                                                tamanho="sm"
                                                onClick={() => onDesativar?.(variacao)}
                                            >
                                                <FiSlash size={13} aria-hidden="true" />
                                                Desativar SKU
                                            </Botao>
                                        ) : (
                                            <Botao
                                                variante="texto"
                                                tamanho="sm"
                                                onClick={() => onAtivar?.(variacao)}
                                            >
                                                <FiRotateCcw size={13} aria-hidden="true" />
                                                Reativar SKU
                                            </Botao>
                                        )}

                                        <Botao
                                            tamanho="sm"
                                            disabled={!alterada || salvando}
                                            carregando={salvando}
                                            onClick={() => salvarLinha(variacao)}
                                        >
                                            <FiCheck size={13} aria-hidden="true" />
                                            Salvar linha
                                        </Botao>
                                    </div>
                                </footer>
                            </article>
                        );
                    })}
            </section>
        </div>
    );
}
