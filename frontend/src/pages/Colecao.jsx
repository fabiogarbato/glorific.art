import { useCallback, useMemo } from "react";
import { useParams, useSearchParams } from "react-router-dom";

import Botao from "@/components/ui/Botao.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Skeleton, { SkeletonCard } from "@/components/ui/Skeleton.jsx";
import CardProduto from "@/components/catalogo/CardProduto.jsx";
import { useCatalogo, useColecao } from "@/hooks/useCatalogo.js";
import {
    filtrosParaSearchParams,
    lerFiltrosDaUrl,
    ORDENACOES,
    TAMANHO_PAGINA_CATALOGO,
} from "@/lib/vitrine.js";
import { formatarData } from "@/utils/datas.js";

/**
 * Landing de colecao: banner, epigrafe e a curadoria do drop.
 *
 * O recorte vem do caminho (/colecao/:slug), entao a query string carrega
 * apenas ordenacao e pagina — a URL continua compartilhavel sem repetir o slug.
 *
 * So colecoes VIGENTES saem do backend. Slug fora do ar cai no 404 editorial
 * daqui, e nao numa tela em branco.
 */
export default function Colecao() {
    const { slug } = useParams();
    const [searchParams, setSearchParams] = useSearchParams();

    const { colecao, naoEncontrada, isLoading: carregandoColecao } = useColecao(slug);

    const filtros = useMemo(
        () => ({ ...lerFiltrosDaUrl(searchParams), colecao: slug ?? "" }),
        [searchParams, slug],
    );

    const { produtos, total, totalPaginas, tamanhoPagina, isLoading, isFetching, isError, refetch } =
        useCatalogo(filtros);

    const alterar = useCallback(
        (parcial) => {
            const proximos = { ...filtros, ...parcial, colecao: "" };
            if (parcial.pagina === undefined) proximos.pagina = 1;
            setSearchParams(filtrosParaSearchParams(proximos));
        },
        [filtros, setSearchParams],
    );

    if (carregandoColecao) {
        return (
            <div>
                <Skeleton className="h-64 w-full sm:h-80" />
                <div className="shell grid grid-cols-2 gap-x-4 gap-y-10 py-16 md:grid-cols-3 lg:grid-cols-4">
                    {Array.from({ length: 8 }).map((_, i) => (
                        <SkeletonCard key={i} />
                    ))}
                </div>
            </div>
        );
    }

    if (naoEncontrada || !colecao) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <p className="eyebrow">Erro 404</p>
                <h1 className="mt-6 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Esta coleção não está no ar.
                </h1>
                <p className="mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                    Ou ela ainda não estreou, ou a vigência terminou. As coleções que estão
                    valendo agora ficam logo abaixo.
                </p>
                <Botao to="/colecoes" className="mt-10">
                    Ver as coleções
                </Botao>
            </div>
        );
    }

    const encerraEm = colecao.dataFim ? formatarData(colecao.dataFim, "") : "";

    return (
        <div className="animate-fade-up">
            {/* ---------------------------------------------------------- BANNER */}
            <section className="relative border-b border-sand bg-linen">
                {colecao.urlMidiaBanner ? (
                    <div className="relative h-72 w-full overflow-hidden sm:h-96">
                        <img
                            src={colecao.urlMidiaBanner}
                            alt={colecao.nome}
                            className="h-full w-full object-cover"
                            loading="eager"
                        />
                        <div
                            aria-hidden="true"
                            className="absolute inset-0 bg-gradient-to-t from-ink/55 via-ink/10 to-transparent"
                        />
                        <div className="absolute inset-x-0 bottom-0">
                            <div className="shell pb-10">
                                <p className="text-xs uppercase tracking-widest text-bone/80">
                                    Coleção
                                </p>
                                <h1 className="mt-3 font-display text-3xl leading-tight tracking-tight text-bone sm:text-4xl">
                                    {colecao.nome}
                                </h1>
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className="shell py-16 text-center">
                        <p className="eyebrow">Coleção</p>
                        <h1 className="mt-4 font-display text-3xl leading-tight tracking-tight text-ink sm:text-4xl">
                            {colecao.nome}
                        </h1>
                    </div>
                )}
            </section>

            {/* -------------------------------------------------------- EPIGRAFE */}
            {(colecao.epigrafe || colecao.descricao) && (
                <section className="shell py-14 text-center">
                    {colecao.epigrafe && (
                        <p className="mx-auto max-w-2xl font-display text-xl italic leading-snug text-ink sm:text-2xl">
                            {colecao.epigrafe}
                        </p>
                    )}
                    {colecao.descricao && (
                        <p className="mx-auto mt-6 max-w-xl text-base leading-relaxed text-ink-soft">
                            {colecao.descricao}
                        </p>
                    )}
                    {encerraEm && (
                        <p className="mt-6 text-xs uppercase tracking-widest text-taupe">
                            Disponível até {encerraEm}
                        </p>
                    )}
                </section>
            )}

            {/* ------------------------------------------------------------ GRID */}
            <section className="shell pb-20">
                <div className="flex flex-wrap items-center justify-between gap-4 border-b border-sand pb-4">
                    <p className="text-xs uppercase tracking-widest text-ink-soft" aria-live="polite">
                        {isLoading
                            ? "Carregando peças…"
                            : total === 0
                              ? "Nenhuma peça"
                              : total === 1
                                ? "1 peça"
                                : `${total} peças`}
                    </p>

                    <div className="flex items-center gap-3">
                        <label htmlFor="ordenacao-linha" className="sr-only">
                            Ordenar por
                        </label>
                        <select
                            id="ordenacao-linha"
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

                <div className="mt-10">
                    {isLoading ? (
                        <div className="grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 lg:grid-cols-4">
                            {Array.from({ length: 8 }).map((_, i) => (
                                <SkeletonCard key={i} />
                            ))}
                        </div>
                    ) : isError ? (
                        <div className="border border-sand bg-linen px-6 py-12 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                As peças desta coleção não carregaram.
                            </p>
                            <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                                Tentar de novo
                            </Botao>
                        </div>
                    ) : produtos.length === 0 ? (
                        <div className="border border-sand bg-linen px-6 py-16 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                Esta coleção está sem peças à venda agora.
                            </p>
                            <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                                Pode ser reposição a caminho. Enquanto isso, o resto da vitrine
                                continua no ar.
                            </p>
                            <Botao to="/catalogo" variante="contorno" className="mt-6">
                                Ver todas as peças
                            </Botao>
                        </div>
                    ) : (
                        <>
                            <div
                                className={`grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 lg:grid-cols-4 ${
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
                                onMudarPagina={(pagina) => {
                                    alterar({ pagina });
                                    window.scrollTo({ top: 0, behavior: "smooth" });
                                }}
                                totalItens={total}
                                itensPorPagina={tamanhoPagina || TAMANHO_PAGINA_CATALOGO}
                            />
                        </>
                    )}
                </div>
            </section>
        </div>
    );
}
