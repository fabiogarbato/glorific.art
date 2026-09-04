import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { FiMinus, FiPlus } from "react-icons/fi";

import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Skeleton, { SkeletonTexto } from "@/components/ui/Skeleton.jsx";
import CardProduto from "@/components/catalogo/CardProduto.jsx";
import CalculadoraFrete from "@/components/catalogo/CalculadoraFrete.jsx";
import EstrelasNota from "@/components/catalogo/EstrelasNota.jsx";
import FormAvaliacao from "@/components/catalogo/FormAvaliacao.jsx";
import GaleriaProduto from "@/components/catalogo/GaleriaProduto.jsx";
import GuiaMedidas from "@/components/catalogo/GuiaMedidas.jsx";
import ListaAvaliacoes from "@/components/catalogo/ListaAvaliacoes.jsx";
import SeletorTamanho from "@/components/catalogo/SeletorTamanho.jsx";
import SwatchCor from "@/components/catalogo/SwatchCor.jsx";

import { useProduto, useRelacionados } from "@/hooks/useCatalogo.js";
import { useCarrinho } from "@/hooks/useCarrinho.js";
import { useToast } from "@/hooks/useToast.js";
import { formatarCentavosParaBRL, formatarParcelamento } from "@/utils/financeiro.js";
import { MAX_PARCELAS, rotuloModelagem } from "@/lib/vitrine.js";

/**
 * Pagina de produto (PDP) — a tela que vende.
 *
 * Decisoes que nao sao estilo:
 *  - a cor escolhida troca a galeria (`produto.galeria` vem agrupada por cor);
 *  - o tamanho SEM saldo aparece desabilitado, nunca escondido;
 *  - o botao de compra so libera com uma variacao (tamanho x cor) escolhida, e
 *    a tela DIZ o que falta em vez de so ficar cinza;
 *  - o frete e cotado por variacao, porque peso e dimensao moram no SKU.
 */
const MAX_POR_COMPRA = 10;

function Detalhe({ titulo, children }) {
    if (!children) return null;
    return (
        <div className="border-t border-sand py-4">
            <dt className="eyebrow">{titulo}</dt>
            <dd className="mt-2 text-sm leading-relaxed text-ink-soft">{children}</dd>
        </div>
    );
}

function EsqueletoProduto() {
    return (
        <div className="shell grid gap-12 py-12 lg:grid-cols-2 lg:py-16">
            <Skeleton className="aspect-product w-full" />
            <div className="flex flex-col gap-5">
                <Skeleton className="h-3 w-24" />
                <Skeleton className="h-8 w-3/4" />
                <Skeleton className="h-5 w-32" />
                <Skeleton className="h-11 w-full" />
                <SkeletonTexto linhas={4} />
            </div>
        </div>
    );
}

export default function Produto() {
    const { slug } = useParams();
    const { produto, naoEncontrado, isLoading, isError, refetch } = useProduto(slug);
    const { relacionados } = useRelacionados(slug, 4);
    const { adicionar, salvando, abrir } = useCarrinho();
    const toast = useToast();

    const [idCorEscolhida, setIdCorEscolhida] = useState(null);
    const [idTamanho, setIdTamanho] = useState(null);
    const [quantidade, setQuantidade] = useState(1);
    const [erroTamanho, setErroTamanho] = useState(null);
    const [guiaAberto, setGuiaAberto] = useState(false);

    const variacoes = useMemo(() => produto?.variacoes ?? [], [produto]);
    const cores = useMemo(() => produto?.cores ?? [], [produto]);

    /** Cor ativa: a escolhida, senao a primeira COM saldo, senao a primeira. */
    const corAtual = useMemo(() => {
        if (!cores.length) return null;
        const escolhida = cores.find((c) => c.id === idCorEscolhida);
        if (escolhida) return escolhida;
        const comSaldo = cores.find((c) =>
            variacoes.some((v) => v.idCor === c.id && v.disponivel),
        );
        return comSaldo ?? cores[0];
    }, [cores, idCorEscolhida, variacoes]);

    /** Grade na ordem do backend, com o estado de cada tamanho NA COR ATUAL. */
    const opcoesTamanho = useMemo(() => {
        const tamanhos = [...(produto?.tamanhos ?? [])].sort((a, b) => a.ordem - b.ordem);

        return tamanhos.map((tamanho) => {
            const variacao = variacoes.find(
                (v) => v.idTamanho === tamanho.id && (!corAtual || v.idCor === corAtual.id),
            );
            return {
                id: tamanho.id,
                codigo: tamanho.codigo,
                existe: !!variacao,
                disponivel: !!variacao?.disponivel,
                quantidadeDisponivel: variacao?.quantidadeDisponivel ?? 0,
            };
        });
    }, [produto, variacoes, corAtual]);

    const variacaoSelecionada = useMemo(
        () =>
            variacoes.find(
                (v) => v.idTamanho === idTamanho && (!corAtual || v.idCor === corAtual.id),
            ) ?? null,
        [variacoes, idTamanho, corAtual],
    );

    /** Fallback para a cotacao de frete enquanto o tamanho nao foi escolhido. */
    const variacaoParaFrete =
        variacaoSelecionada ??
        variacoes.find((v) => v.disponivel && (!corAtual || v.idCor === corAtual.id)) ??
        variacoes.find((v) => v.disponivel) ??
        null;

    const midias = useMemo(() => {
        const galeria = produto?.galeria ?? [];
        if (!galeria.length) return [];
        const daCor = galeria.find((g) => corAtual && g.idCor === corAtual.id);
        if (daCor?.midias?.length) return daCor.midias;
        const neutro = galeria.find((g) => g.idCor === null || g.idCor === undefined);
        if (neutro?.midias?.length) return neutro.midias;
        return galeria.flatMap((g) => g.midias ?? []);
    }, [produto, corAtual]);

    if (isLoading) return <EsqueletoProduto />;

    if (isError) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <h1 className="font-display text-2xl tracking-tight text-ink">
                    Não conseguimos abrir esta peça.
                </h1>
                <p className="mt-4 max-w-md text-sm leading-relaxed text-ink-soft">
                    Pode ter sido a conexão. Tente de novo em instantes.
                </p>
                <Botao variante="contorno" className="mt-8" onClick={() => refetch()}>
                    Tentar de novo
                </Botao>
            </div>
        );
    }

    if (naoEncontrado || !produto) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <p className="eyebrow">Erro 404</p>
                <h1 className="mt-6 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Esta peça saiu de catálogo.
                </h1>
                <p className="mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                    O endereço não existe mais, ou a peça foi despublicada. Veja o que está no
                    ar agora.
                </p>
                <Botao to="/catalogo" className="mt-10">
                    Ver a vitrine
                </Botao>
            </div>
        );
    }

    const precoAtual = variacaoSelecionada?.precoCentavos ?? produto.precoAPartirDeCentavos;
    const emOferta =
        !!produto.precoComparativoCentavos && produto.precoComparativoCentavos > precoAtual;
    const precosVariam = variacoes.some((v) => v.precoCentavos !== precoAtual);
    const maximo = Math.min(MAX_POR_COMPRA, variacaoSelecionada?.quantidadeDisponivel || 1);
    const temTabela = !!produto.tabelaMedidas?.linhas?.length;

    function escolherCor(cor) {
        setIdCorEscolhida(cor.id);
        // O tamanho escolhido pode nao existir na nova cor: nao carregamos uma
        // selecao invalida adiante, o cliente escolhe de novo com a grade certa.
        const aindaVale = variacoes.some(
            (v) => v.idCor === cor.id && v.idTamanho === idTamanho && v.disponivel,
        );
        if (!aindaVale) setIdTamanho(null);
        setQuantidade(1);
    }

    function escolherTamanho(opcao) {
        setIdTamanho(opcao.id);
        setErroTamanho(null);
        setQuantidade(1);
    }

    async function adicionarNaSacola() {
        if (!variacaoSelecionada) {
            setErroTamanho("Escolha um tamanho para adicionar à sacola.");
            return;
        }
        try {
            await adicionar({ idVariacao: variacaoSelecionada.id }, quantidade);
            toast.success("Peça adicionada à sacola.");
            abrir();
        } catch {
            // O interceptor do axios ja mostrou a mensagem do servidor.
        }
    }

    return (
        <div className="animate-fade-up">
            <div className="shell py-8 lg:py-12">
                {/* --------------------------------------------------- MIGALHAS */}
                <nav aria-label="Você está aqui" className="text-xs text-taupe">
                    <ol className="flex flex-wrap items-center gap-2">
                        <li>
                            <Link to="/" className="transition-colors hover:text-ink">
                                Início
                            </Link>
                        </li>
                        <li aria-hidden="true">/</li>
                        {produto.slugCategoria && (
                            <>
                                <li>
                                    <Link
                                        to={`/categoria/${produto.slugCategoria}`}
                                        className="transition-colors hover:text-ink"
                                    >
                                        {produto.nomeCategoria}
                                    </Link>
                                </li>
                                <li aria-hidden="true">/</li>
                            </>
                        )}
                        <li className="text-ink-soft" aria-current="page">
                            {produto.nome}
                        </li>
                    </ol>
                </nav>

                <div className="mt-8 grid gap-10 lg:grid-cols-2 lg:gap-16">
                    {/* ------------------------------------------------ GALERIA */}
                    <GaleriaProduto midias={midias} nome={produto.nome} />

                    {/* -------------------------------------------------- COMPRA */}
                    <div className="lg:sticky lg:top-28 lg:self-start">
                        {produto.esgotado && (
                            <Badge variante="esgotado" className="mb-4">
                                Esgotado
                            </Badge>
                        )}

                        <h1 className="font-display text-2xl leading-tight tracking-tight text-ink sm:text-3xl">
                            {produto.nome}
                        </h1>

                        {produto.totalAvaliacoes > 0 && (
                            <a
                                href="#avaliacoes"
                                className="mt-4 inline-flex items-center gap-2 text-xs text-ink-soft underline-offset-4 hover:underline"
                            >
                                <EstrelasNota
                                    nota={produto.notaMedia}
                                    total={produto.totalAvaliacoes}
                                    tamanho={14}
                                />
                                <span>
                                    {produto.totalAvaliacoes === 1
                                        ? "1 avaliação"
                                        : `${produto.totalAvaliacoes} avaliações`}
                                </span>
                            </a>
                        )}

                        {/* ---------------------------------------------- PRECO */}
                        <div className="mt-6">
                            <p className="preco flex flex-wrap items-baseline gap-3">
                                {emOferta && (
                                    <span className="text-sm text-taupe line-through">
                                        {formatarCentavosParaBRL(
                                            produto.precoComparativoCentavos,
                                        )}
                                    </span>
                                )}
                                <span
                                    className={`text-xl ${emOferta ? "text-clay" : "text-ink"}`}
                                >
                                    {!variacaoSelecionada && precosVariam && (
                                        <span className="mr-2 text-xs uppercase tracking-widest text-taupe">
                                            A partir de
                                        </span>
                                    )}
                                    {formatarCentavosParaBRL(precoAtual)}
                                </span>
                            </p>

                            <p className="mt-2 text-xs text-ink-soft">
                                Em até {formatarParcelamento(precoAtual, MAX_PARCELAS)} sem juros
                            </p>
                        </div>

                        {/* ------------------------------------------------ COR */}
                        {cores.length > 0 && (
                            <div className="mt-8">
                                <p className="eyebrow">
                                    Cor{corAtual ? `: ${corAtual.nome}` : ""}
                                </p>
                                <div className="mt-3 flex flex-wrap gap-3">
                                    {cores.map((cor) => (
                                        <SwatchCor
                                            key={cor.id}
                                            cor={cor}
                                            selecionada={corAtual?.id === cor.id}
                                            onSelecionar={escolherCor}
                                            indisponivel={
                                                !variacoes.some(
                                                    (v) => v.idCor === cor.id && v.disponivel,
                                                )
                                            }
                                        />
                                    ))}
                                </div>
                            </div>
                        )}

                        {/* -------------------------------------------- TAMANHO */}
                        <div className="mt-8">
                            <div className="flex items-baseline justify-between gap-4">
                                <p className="eyebrow">Tamanho</p>
                                {temTabela && (
                                    <button
                                        type="button"
                                        onClick={() => setGuiaAberto(true)}
                                        className="text-xs uppercase tracking-widest text-ink-soft underline underline-offset-4 transition-colors hover:text-ink"
                                    >
                                        Guia de medidas
                                    </button>
                                )}
                            </div>

                            <div className="mt-3">
                                <SeletorTamanho
                                    opcoes={opcoesTamanho}
                                    idSelecionado={idTamanho}
                                    onSelecionar={escolherTamanho}
                                    erro={erroTamanho}
                                />
                            </div>
                        </div>

                        {/* ----------------------------------------- QUANTIDADE */}
                        <div className="mt-8 flex flex-wrap items-end gap-4">
                            <div>
                                <p className="eyebrow">Quantidade</p>
                                <div className="mt-3 inline-flex items-center border border-sand">
                                    <button
                                        type="button"
                                        aria-label="Diminuir quantidade"
                                        disabled={quantidade <= 1}
                                        onClick={() => setQuantidade((q) => Math.max(1, q - 1))}
                                        className="flex h-11 w-11 items-center justify-center text-ink transition-colors hover:bg-linen disabled:opacity-35"
                                    >
                                        <FiMinus size={14} aria-hidden="true" />
                                    </button>
                                    <span
                                        className="w-10 text-center text-sm tabular-nums text-ink"
                                        aria-live="polite"
                                    >
                                        {quantidade}
                                    </span>
                                    <button
                                        type="button"
                                        aria-label="Aumentar quantidade"
                                        disabled={!variacaoSelecionada || quantidade >= maximo}
                                        onClick={() =>
                                            setQuantidade((q) => Math.min(maximo, q + 1))
                                        }
                                        className="flex h-11 w-11 items-center justify-center text-ink transition-colors hover:bg-linen disabled:opacity-35"
                                    >
                                        <FiPlus size={14} aria-hidden="true" />
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* --------------------------------------------- COMPRA */}
                        <div className="mt-8">
                            <Botao
                                tamanho="lg"
                                blocoCompleto
                                carregando={salvando}
                                disabled={produto.esgotado || !variacaoSelecionada}
                                onClick={adicionarNaSacola}
                            >
                                {produto.esgotado ? "Peça esgotada" : "Adicionar à sacola"}
                            </Botao>

                            <p className="mt-3 text-xs text-ink-soft" aria-live="polite">
                                {produto.esgotado
                                    ? "Esta peça está sem estoque em todos os tamanhos."
                                    : variacaoSelecionada
                                      ? "Reservamos a peça só depois do pagamento aprovado."
                                      : "Escolha o tamanho para liberar o botão."}
                            </p>
                        </div>

                        {/* ---------------------------------------------- FRETE */}
                        <div className="mt-8">
                            <CalculadoraFrete
                                variacao={variacaoParaFrete}
                                quantidade={variacaoSelecionada ? quantidade : 1}
                                tamanhoEscolhido={!!variacaoSelecionada}
                            />
                        </div>

                        {/* -------------------------------------------- DETALHES */}
                        <dl className="mt-10">
                            <Detalhe titulo="Composição">{produto.composicaoTecido}</Detalhe>
                            <Detalhe titulo="Cuidados">{produto.instrucoesLavagem}</Detalhe>
                            <Detalhe titulo="Modelagem">
                                {rotuloModelagem(produto.modelagem)}
                            </Detalhe>
                            <Detalhe titulo="Referência">{produto.skuBase}</Detalhe>
                        </dl>
                    </div>
                </div>
            </div>

            {/* ------------------------------------------------------ DESCRICAO */}
            {produto.descricao && (
                <section className="border-y border-sand bg-linen">
                    <div className="shell py-14">
                        <div className="max-w-2xl">
                            <p className="eyebrow">Sobre a peça</p>
                            <p className="mt-5 whitespace-pre-line text-base leading-relaxed text-ink-soft">
                                {produto.descricao}
                            </p>
                        </div>
                    </div>
                </section>
            )}

            {/* ----------------------------------------------------- AVALIACOES */}
            <section id="avaliacoes" className="shell py-16 lg:py-20">
                <div className="mb-10 max-w-2xl">
                    <p className="eyebrow">Quem vestiu</p>
                    <h2 className="mt-3 font-display text-2xl tracking-tight text-ink">
                        Avaliações
                    </h2>
                </div>

                <div className="grid gap-12 lg:grid-cols-[1fr_20rem] lg:gap-16">
                    <ListaAvaliacoes idProduto={produto.id} />
                    <div className="lg:pt-1">
                        <FormAvaliacao idProduto={produto.id} tamanhos={produto.tamanhos} />
                    </div>
                </div>
            </section>

            {/* ---------------------------------------------------- RELACIONADOS */}
            {relacionados.length > 0 && (
                <section className="border-t border-sand">
                    <div className="shell py-16">
                        <p className="eyebrow">Combina com</p>
                        <h2 className="mt-3 font-display text-2xl tracking-tight text-ink">
                            Você também pode gostar
                        </h2>

                        <div className="mt-10 grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 lg:grid-cols-4">
                            {relacionados.map((item) => (
                                <CardProduto key={item.id} produto={item} />
                            ))}
                        </div>
                    </div>
                </section>
            )}

            <GuiaMedidas
                tabela={produto.tabelaMedidas}
                isOpen={guiaAberto}
                onClose={() => setGuiaAberto(false)}
            />
        </div>
    );
}
