import { FiArrowRight } from "react-icons/fi";
import { Link } from "react-router-dom";

import Botao from "@/components/ui/Botao.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Skeleton, { SkeletonCard } from "@/components/ui/Skeleton.jsx";
import CardProduto from "@/components/catalogo/CardProduto.jsx";
import { useCatalogo, useColecoes, useDestaques } from "@/hooks/useCatalogo.js";
import { STORE } from "@/data/store.js";

/**
 * Home editorial.
 *
 * O hero e a voz da marca e continua estatico de proposito — quem escreve o
 * manifesto e o marketing, nao o cadastro. Da faixa de destaques para baixo
 * TUDO vem da API: vitrine com `destaque=true` e a colecao marcada como
 * destaque no admin.
 */
const PILARES = [
    {
        titulo: "Tecido natural",
        texto: "Linho, algodão egípcio e viscose de origem responsável. Nada de brilho sintético.",
    },
    {
        titulo: "Modelagem serena",
        texto: "Caimento que cobre sem esconder. Comprimento pensado para o corpo em movimento.",
    },
    {
        titulo: "Produção curta",
        texto: "Lotes pequenos, costura nacional e reposição só quando a peça merece voltar.",
    },
];

export default function Home() {
    const { destaques, isLoading: carregandoDestaques, isError: erroDestaques } = useDestaques(8);
    const { colecoes, isLoading: carregandoColecoes } = useColecoes();

    // A colecao em destaque e a marcada no admin; sem ela, a primeira vigente.
    const colecaoDestaque = colecoes.find((c) => c.destaque) ?? colecoes[0] ?? null;

    // Sem banner de colecao, o hero usa a capa da ULTIMA peca publicada — e o
    // mesmo criterio do link "Novidades" do menu (ordenacao "Novidade").
    const precisaUltimaPeca = !colecaoDestaque?.urlMidiaBanner && !colecaoDestaque?.urlMidiaCapa;
    const { produtos: ultimasPecas } = useCatalogo(
        { ordenacao: "Novidade" },
        { pageSize: 1 },
    );
    const ultimaPeca = precisaUltimaPeca ? (ultimasPecas[0] ?? null) : null;

    return (
        <div className="animate-fade-up">
            {/* ---------------------------------------------------------- HERO */}
            <section className="border-b border-sand bg-linen">
                <div className="shell grid items-center gap-12 py-16 lg:grid-cols-12 lg:items-start lg:py-24">
                    <div className="lg:col-span-6">
                        {carregandoColecoes ? (
                            <Skeleton className="h-3 w-48" />
                        ) : (
                            <p className="eyebrow">
                                {colecaoDestaque
                                    ? `Em cartaz · ${colecaoDestaque.nome}`
                                    : STORE.tagline}
                            </p>
                        )}

                        <h1 className="mt-6 font-display text-3xl leading-[1.05] tracking-tight text-ink sm:text-4xl">
                            A beleza que
                            <br />
                            <em className="font-normal italic text-olive">não precisa</em>
                            <br />
                            gritar.
                        </h1>

                        <p className="mt-8 max-w-md text-base leading-relaxed text-ink-soft">
                            {STORE.manifesto} Peças desenhadas para durar mais de uma estação,
                            e para vestir bem tanto no domingo quanto na terça-feira.
                        </p>

                        <div className="mt-10 flex flex-wrap items-center gap-4">
                            <Botao to="/catalogo" tamanho="lg">
                                Ver a coleção <FiArrowRight size={14} aria-hidden="true" />
                            </Botao>
                            <Botao to="/sobre" variante="texto" tamanho="lg">
                                Nossa história
                            </Botao>
                        </div>

                        <div className="mt-12 flex flex-wrap items-center gap-x-8 gap-y-3">
                            <span className="text-xs uppercase tracking-widest text-taupe">
                                Frete cortesia acima de R$ 399
                            </span>
                            <span className="text-xs uppercase tracking-widest text-taupe">
                                Troca gratuita em 30 dias
                            </span>
                        </div>
                    </div>

                    {/* Bloco de imagem: banner da colecao em destaque > capa da ultima peca
                        publicada > degrade da paleta com a epigrafe (quando o catalogo
                        ainda esta vazio de verdade). */}
                    <div className="lg:col-span-6">
                        <div className="relative">
                            {colecaoDestaque?.urlMidiaBanner || colecaoDestaque?.urlMidiaCapa ? (
                                <Link
                                    to={`/colecao/${colecaoDestaque.slug}`}
                                    className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-4 focus-visible:ring-offset-base-200"
                                >
                                    <div className="aspect-product w-full overflow-hidden lg:aspect-auto lg:h-[520px]">
                                        <img
                                            src={
                                                colecaoDestaque.urlMidiaBanner ||
                                                colecaoDestaque.urlMidiaCapa
                                            }
                                            alt={colecaoDestaque.nome}
                                            loading="eager"
                                            className="h-full w-full object-cover"
                                        />
                                    </div>
                                </Link>
                            ) : ultimaPeca ? (
                                <Link
                                    to={`/produto/${ultimaPeca.slug}`}
                                    className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-4 focus-visible:ring-offset-base-200"
                                >
                                    <div className="aspect-product w-full overflow-hidden lg:aspect-auto lg:h-[520px]">
                                        <img
                                            src={ultimaPeca.urlImagemCapa}
                                            alt={ultimaPeca.altImagemCapa || ultimaPeca.nome}
                                            loading="eager"
                                            className="h-full w-full object-cover"
                                        />
                                    </div>
                                </Link>
                            ) : (
                                <>
                                    <div className="aspect-product w-full bg-gradient-to-b from-sand via-linen to-bone lg:aspect-auto lg:h-[520px]" />
                                    <div className="absolute inset-0 flex flex-col items-center justify-center px-8 text-center">
                                        <span className="font-display text-4xl leading-none text-ink/15">
                                            ✦
                                        </span>
                                        <p className="mt-6 max-w-xs font-display text-xl italic leading-snug text-ink/70">
                                            {colecaoDestaque?.epigrafe ??
                                                "“Que a graça esteja no detalhe, e o detalhe no silêncio.”"}
                                        </p>
                                    </div>
                                </>
                            )}

                            <div className="absolute left-4 top-4">
                                <Badge variante="destaque">Novo</Badge>
                            </div>
                        </div>
                    </div>
                </div>
            </section>

            {/* -------------------------------------------------------- PILARES */}
            <section className="shell py-16 lg:py-20">
                <div className="grid gap-10 sm:grid-cols-3">
                    {PILARES.map((p) => (
                        <article key={p.titulo}>
                            <div className="filete" />
                            <h2 className="mt-6 font-display text-xl tracking-tight text-ink">
                                {p.titulo}
                            </h2>
                            <p className="mt-3 text-sm leading-relaxed text-ink-soft">{p.texto}</p>
                        </article>
                    ))}
                </div>
            </section>

            {/* -------------------------------------------------------- VITRINE */}
            <section className="shell pb-20">
                <div className="mb-10 flex items-end justify-between gap-6">
                    <div>
                        <p className="eyebrow">Seleção da casa</p>
                        <h2 className="mt-3 font-display text-2xl tracking-tight text-ink">
                            Peças que voltam sempre
                        </h2>
                    </div>
                    <Botao to="/catalogo" variante="texto" tamanho="sm" className="shrink-0">
                        Ver tudo
                    </Botao>
                </div>

                {/* 2 col mobile / 3 tablet / 4 desktop, gap-x-4 gap-y-10 — o respiro
                    separa os cards, por isso nao ha borda nem sombra. */}
                {carregandoDestaques ? (
                    <div className="grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 lg:grid-cols-4">
                        {Array.from({ length: 4 }).map((_, i) => (
                            <SkeletonCard key={i} />
                        ))}
                    </div>
                ) : erroDestaques ? (
                    <p className="text-sm text-ink-soft">
                        A vitrine não carregou agora. Atualize a página para tentar de novo.
                    </p>
                ) : destaques.length === 0 ? (
                    <div className="border border-sand bg-linen px-6 py-14 text-center">
                        <p className="font-display text-xl tracking-tight text-ink">
                            Nenhuma peça em destaque no momento.
                        </p>
                        <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                            A vitrine completa continua aberta. É lá que está tudo o que dá para
                            levar hoje.
                        </p>
                        <Botao to="/catalogo" variante="contorno" className="mt-6">
                            Ver todas as peças
                        </Botao>
                    </div>
                ) : (
                    <div className="grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 lg:grid-cols-4">
                        {destaques.map((produto, i) => (
                            <CardProduto
                                key={produto.id}
                                produto={produto}
                                carregamentoAntecipado={i < 4}
                            />
                        ))}
                    </div>
                )}
            </section>

            {/* ------------------------------------------------ COLECAO EM FOCO */}
            {colecaoDestaque && (
                <section className="border-y border-sand bg-linen">
                    <div className="shell grid items-center gap-10 py-16 lg:grid-cols-12 lg:py-20">
                        <div className="lg:col-span-5">
                            <p className="eyebrow">Coleção em destaque</p>
                            <h2 className="mt-4 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                                {colecaoDestaque.nome}
                            </h2>
                            {colecaoDestaque.epigrafe && (
                                <p className="mt-5 font-display text-xl italic leading-snug text-ink-soft">
                                    {colecaoDestaque.epigrafe}
                                </p>
                            )}
                            {colecaoDestaque.descricao && (
                                <p className="mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                                    {colecaoDestaque.descricao}
                                </p>
                            )}
                            <Botao
                                to={`/colecao/${colecaoDestaque.slug}`}
                                variante="contorno"
                                className="mt-8"
                            >
                                Ver a coleção
                            </Botao>
                        </div>

                        <div className="lg:col-span-7">
                            {colecaoDestaque.urlMidiaCapa || colecaoDestaque.urlMidiaBanner ? (
                                <div className="aspect-[4/3] w-full overflow-hidden">
                                    <img
                                        src={
                                            colecaoDestaque.urlMidiaCapa ||
                                            colecaoDestaque.urlMidiaBanner
                                        }
                                        alt={colecaoDestaque.nome}
                                        loading="lazy"
                                        className="h-full w-full object-cover"
                                    />
                                </div>
                            ) : (
                                <div className="aspect-[4/3] w-full bg-gradient-to-b from-sand via-linen to-bone" />
                            )}
                        </div>
                    </div>
                </section>
            )}

            {/* --------------------------------------------------------- MANIFESTO */}
            <section className="border-b border-sand">
                <div className="shell py-20 text-center">
                    <p className="eyebrow">O propósito</p>
                    <p className="mx-auto mt-6 max-w-2xl font-display text-2xl italic leading-snug text-ink">
                        Vestir com dignidade é um gesto de fé, e não precisa de estampa
                        para ser dito.
                    </p>
                    <div className="mt-10 flex justify-center">
                        <Botao to="/colecoes" variante="contorno">
                            Conhecer as coleções
                        </Botao>
                    </div>
                </div>
            </section>
        </div>
    );
}
