import { useCallback, useMemo, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { FiSliders, FiX } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import { SkeletonCard } from "@/components/ui/Skeleton.jsx";
import CardProduto from "@/components/catalogo/CardProduto.jsx";
import FiltrosCatalogo from "@/components/catalogo/FiltrosCatalogo.jsx";
import { useCatalogo, useCategoria, useFacetasCatalogo } from "@/hooks/useCatalogo.js";
import { useDialog } from "@/hooks/useDialog.js";
import {
    contarFiltrosAtivos,
    filtrosParaSearchParams,
    lerFiltrosDaUrl,
    ORDENACOES,
    TAMANHO_PAGINA_CATALOGO,
} from "@/lib/vitrine.js";

/**
 * Vitrine paginada — serve /catalogo, /busca e /categoria/:slug.
 *
 * O ESTADO MORA NA URL. Nada de useState paralelo para filtro: o link tem que
 * poder ser colado no WhatsApp e abrir exatamente a mesma tela, e o botao
 * "voltar" do navegador tem que desfazer o ultimo filtro.
 */
function contadorDeResultados(total, carregando) {
    if (carregando) return "Carregando peças…";
    if (total === 0) return "Nenhuma peça";
    if (total === 1) return "1 peça";
    return `${total} peças`;
}

function DrawerFiltros({ aberto, aoFechar, children }) {
    const { panelRef, dialogProps } = useDialog(aberto, aoFechar);

    if (!aberto) return null;

    return (
        <div className="fixed inset-0 z-overlay lg:hidden">
            <button
                type="button"
                aria-label="Fechar filtros"
                tabIndex={-1}
                onClick={aoFechar}
                className="absolute inset-0 h-full w-full cursor-default bg-ink/40"
            />

            <div
                ref={panelRef}
                {...dialogProps}
                aria-label="Filtros da vitrine"
                className="absolute inset-y-0 left-0 flex w-[88%] max-w-sm flex-col border-r border-sand bg-base-100"
            >
                <header className="flex items-center justify-between border-b border-sand px-5 py-4">
                    <h2 className="font-display text-xl tracking-tight text-ink">Filtros</h2>
                    <button
                        type="button"
                        onClick={aoFechar}
                        aria-label="Fechar filtros"
                        className="-mr-2 flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-ink"
                    >
                        <FiX size={18} aria-hidden="true" />
                    </button>
                </header>

                <div className="flex-1 overflow-y-auto px-5 py-6">{children}</div>

                <footer className="border-t border-sand px-5 py-4">
                    <Botao blocoCompleto onClick={aoFechar}>
                        Ver resultados
                    </Botao>
                </footer>
            </div>
        </div>
    );
}

export default function Catalogo({ modo = "vitrine" }) {
    const [searchParams, setSearchParams] = useSearchParams();
    const { slug } = useParams();
    const [drawerAberto, setDrawerAberto] = useState(false);

    const ehCategoria = modo === "categoria";
    const ehBusca = modo === "busca";

    const filtrosDaUrl = lerFiltrosDaUrl(searchParams);

    // Na rota /categoria/:slug o recorte vem do caminho, nao da query string.
    const filtros = useMemo(
        () => (ehCategoria ? { ...filtrosDaUrl, categoria: slug ?? "" } : filtrosDaUrl),
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [searchParams, slug, ehCategoria],
    );

    const { categoria } = useCategoria(ehCategoria ? slug : null);

    const {
        produtos,
        total,
        totalPaginas,
        tamanhoPagina,
        isLoading,
        isFetching,
        isError,
        refetch,
    } = useCatalogo(filtros);

    const { facetas, isLoading: carregandoFacetas } = useFacetasCatalogo(filtros);

    /** Toda alteracao de filtro volta para a pagina 1 — senao some resultado. */
    const alterar = useCallback(
        (parcial) => {
            const proximos = { ...filtros, ...parcial };
            if (parcial.pagina === undefined) proximos.pagina = 1;
            if (ehCategoria) proximos.categoria = "";
            setSearchParams(filtrosParaSearchParams(proximos));
        },
        [filtros, ehCategoria, setSearchParams],
    );

    const limpar = useCallback(() => {
        const preservado = filtros.busca ? { busca: filtros.busca } : {};
        setSearchParams(filtrosParaSearchParams({ ...preservado, pagina: 1 }));
    }, [filtros.busca, setSearchParams]);

    const irParaPagina = useCallback(
        (pagina) => {
            alterar({ pagina });
            window.scrollTo({ top: 0, behavior: "smooth" });
        },
        [alterar],
    );

    /**
     * Na rota /categoria/:slug a categoria nao e um filtro que da para tirar —
     * ela e o proprio endereco. Some da contagem e do painel para o "Limpar"
     * nao prometer desfazer o que nao desfaz.
     */
    const filtrosVisiveis = ehCategoria ? { ...filtros, categoria: "" } : filtros;
    const ativos = contarFiltrosAtivos(filtrosVisiveis);

    const titulo = ehBusca
        ? filtros.busca
            ? `Resultados para “${filtros.busca}”`
            : "Buscar peças"
        : ehCategoria
          ? (categoria?.nome ?? "Categoria")
          : "Todas as peças";

    const chapeu = ehBusca ? "Busca" : ehCategoria ? "Categoria" : "Vitrine";

    const painelFiltros = (
        <FiltrosCatalogo
            facetas={facetas}
            filtros={filtrosVisiveis}
            onAlterar={alterar}
            onLimpar={limpar}
            carregando={carregandoFacetas}
            esconderCategoria={ehCategoria}
        />
    );

    return (
        <div className="shell py-12 lg:py-16">
            {/* ------------------------------------------------------ CABECALHO */}
            <header className="max-w-2xl">
                <p className="eyebrow">{chapeu}</p>
                <h1 className="mt-4 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    {titulo}
                </h1>
                {ehCategoria && categoria?.descricao && (
                    <p className="mt-4 text-base leading-relaxed text-ink-soft">
                        {categoria.descricao}
                    </p>
                )}
            </header>

            {/* ------------------------------------------- BARRA DE FERRAMENTAS */}
            <div className="mt-10 flex flex-wrap items-center justify-between gap-4 border-b border-sand pb-4">
                <p className="text-xs uppercase tracking-widest text-ink-soft" aria-live="polite">
                    {contadorDeResultados(total, isLoading)}
                </p>

                <div className="flex items-center gap-3">
                    <button
                        type="button"
                        onClick={() => setDrawerAberto(true)}
                        className="inline-flex h-10 items-center gap-2 border border-sand px-4 text-xs uppercase tracking-widest text-ink transition-colors hover:border-ink lg:hidden"
                    >
                        <FiSliders size={14} aria-hidden="true" />
                        Filtrar
                        {ativos > 0 && (
                            <span className="ml-1 bg-ink px-1.5 text-[11px] tabular-nums text-bone">
                                {ativos}
                            </span>
                        )}
                    </button>

                    <label htmlFor="ordenacao-catalogo" className="sr-only">
                        Ordenar por
                    </label>
                    <select
                        id="ordenacao-catalogo"
                        value={filtros.ordenacao}
                        onChange={(e) => alterar({ ordenacao: e.target.value })}
                        className="h-10 border border-sand bg-base-100 px-3 text-xs uppercase tracking-widest text-ink focus:border-olive focus:outline-none"
                    >
                        {ORDENACOES.map((o) => (
                            <option key={o.valor} value={o.valor}>
                                {o.rotulo}
                            </option>
                        ))}
                    </select>
                </div>
            </div>

            {/* ------------------------------------------------- FILTROS + GRID */}
            <div className="mt-10 grid gap-10 lg:grid-cols-[16rem_1fr] lg:gap-14">
                <aside className="hidden lg:block">
                    <div className="sticky top-28">{painelFiltros}</div>
                </aside>

                <section aria-busy={isFetching || undefined}>
                    {isLoading ? (
                        <div className="grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 xl:grid-cols-4">
                            {Array.from({ length: 8 }).map((_, i) => (
                                <SkeletonCard key={i} />
                            ))}
                        </div>
                    ) : isError ? (
                        <div className="border border-sand bg-linen px-6 py-12 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                A vitrine não carregou.
                            </p>
                            <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                                Pode ter sido a conexão. Tente de novo. Se insistir, avise a
                                gente pelo WhatsApp.
                            </p>
                            <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                                Tentar de novo
                            </Botao>
                        </div>
                    ) : produtos.length === 0 ? (
                        <div className="border border-sand bg-linen px-6 py-16 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                Nenhuma peça com esse recorte.
                            </p>
                            <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                                {ativos > 0
                                    ? "Tente soltar um filtro: o tamanho e a faixa de preço costumam ser os mais restritivos."
                                    : filtros.busca
                                      ? "Nada com esse termo. Tente uma palavra mais curta, como “vestido” ou “linho”."
                                      : "Ainda não há peças publicadas aqui. Volte em alguns dias."}
                            </p>
                            {ativos > 0 && (
                                <Botao variante="contorno" className="mt-6" onClick={limpar}>
                                    Limpar filtros
                                </Botao>
                            )}
                        </div>
                    ) : (
                        <>
                            <div
                                className={`grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 xl:grid-cols-4 ${
                                    isFetching ? "opacity-60 transition-opacity" : ""
                                }`}
                            >
                                {produtos.map((produto, i) => (
                                    <CardProduto
                                        key={produto.id}
                                        produto={produto}
                                        carregamentoAntecipado={i < 4}
                                    />
                                ))}
                            </div>

                            <Paginacao
                                className="mt-14 border-t border-sand pt-6"
                                paginaAtual={filtros.pagina}
                                totalPaginas={totalPaginas}
                                onMudarPagina={irParaPagina}
                                totalItens={total}
                                itensPorPagina={tamanhoPagina || TAMANHO_PAGINA_CATALOGO}
                            />
                        </>
                    )}
                </section>
            </div>

            <DrawerFiltros aberto={drawerAberto} aoFechar={() => setDrawerAberto(false)}>
                {painelFiltros}
            </DrawerFiltros>
        </div>
    );
}
