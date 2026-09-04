import { FiArrowRight, FiRepeat, FiShield, FiTruck } from "react-icons/fi";
import { Link } from "react-router-dom";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton, { SkeletonCard } from "@/components/ui/Skeleton.jsx";
import CardProduto from "@/components/catalogo/CardProduto.jsx";
import { useColecoes, useDestaques } from "@/hooks/useCatalogo.js";

/**
 * Home editorial.
 *
 * O hero e a voz da marca e continua estatico de proposito — quem escreve o
 * manifesto e o marketing, nao o cadastro. Da faixa de destaques para baixo
 * TUDO vem da API: vitrine com `destaque=true` e a colecao marcada como
 * destaque no admin.
 */
const GARANTIAS_HERO = [
    { Icone: FiTruck, texto: "Frete cortesia acima de R$ 399" },
    { Icone: FiRepeat, texto: "Troca gratuita em 30 dias" },
    { Icone: FiShield, texto: "Compra segura e sem complicação" },
];

const PILARES = [
    {
        titulo: "Oversized de verdade",
        texto: "Algodão penteado 220g, corte largo, ombro caído. Pesa na mão e cai no corpo do jeito que camiseta de rua tem que cair.",
    },
    {
        titulo: "A mensagem vai no peito",
        texto: "Cada estampa carrega um versículo ou uma imagem que diz alguma coisa. Nada de logo gigante, nada de frase de para-choque.",
    },
    {
        titulo: "Lote pequeno",
        texto: "A gente produz pouco de cada vez. Quando acaba, acaba. Só volta o que ainda vale a pena repetir.",
    },
];

export default function Home() {
    const { destaques, isLoading: carregandoDestaques, isError: erroDestaques } = useDestaques(8);
    const { colecoes, isLoading: carregandoColecoes } = useColecoes();

    // A colecao em destaque e a marcada no admin; sem ela, a primeira vigente.
    const colecaoDestaque = colecoes.find((c) => c.destaque) ?? colecoes[0] ?? null;

    return (
        <div className="animate-fade-up">
            {/* ---------------------------------------------------------- HERO
                Imagem de campanha fixa (fora do CMS de proposito, e arte de
                marca, nao cadastro) — o "A arte de glorificar" e a assinatura
                da marca ja vem impressos na propria foto, por isso o texto
                real fica so a partir do titulo. */}
            <section className="relative overflow-hidden bg-ink">
                <img
                    src="/hero-wall-bg.jpg"
                    alt="Modelo vestindo camiseta oversized Glorific, parede com silhueta de igreja e skyline ao fundo"
                    loading="eager"
                    className="absolute inset-0 h-full w-full object-cover object-[50%_38%] lg:object-[50%_20%]"
                />
                <div className="absolute inset-0 bg-gradient-to-r from-ink via-ink/80 to-ink/30 lg:via-ink/55 lg:to-transparent" />
                {/* Fade no topo — cobre o resquico do texto "glorificar" gravado
                    na propria foto, que sem isso brigava com o logo real. */}
                <div className="absolute inset-x-0 top-0 h-24 bg-gradient-to-b from-ink to-transparent" />

                <div className="shell relative py-10 lg:py-28">
                    <div className="max-w-lg">
                        {!carregandoColecoes && colecaoDestaque && (
                            <p className="font-sans text-xs uppercase tracking-widest text-brass">
                                Em cartaz · {colecaoDestaque.nome}
                            </p>
                        )}

                        <h1 className="font-display text-4xl leading-[1.05] tracking-tight text-bone sm:text-5xl lg:text-6xl">
                            Peças de rua
                            <br />
                            com <em className="font-normal italic text-olive">propósito</em>.
                        </h1>

                        <p className="mt-6 max-w-md text-base leading-relaxed text-bone/70 lg:mt-8">
                            Corte oversized em algodão 220g, feita em lote pequeno. Ideal para
                            usar todo dia, no culto ou na rua.
                        </p>

                        <div className="mt-8 flex flex-wrap items-center gap-6 lg:mt-10">
                            <Botao
                                to="/catalogo"
                                tamanho="lg"
                                className="focus-visible:ring-offset-ink"
                            >
                                Ver a coleção <FiArrowRight size={14} aria-hidden="true" />
                            </Botao>
                            <Link
                                to="/sobre"
                                className="text-xs font-sans uppercase tracking-widest text-bone/70 underline underline-offset-4 decoration-bone/30 transition-colors hover:text-bone hover:decoration-bone/70"
                            >
                                Nossa história
                            </Link>
                        </div>

                        <div className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-4 border-t border-bone/15 pt-6 lg:mt-14 lg:pt-8">
                            {GARANTIAS_HERO.map(({ Icone, texto }) => (
                                <span
                                    key={texto}
                                    className="flex items-center gap-2 text-[11px] uppercase tracking-widest text-bone/60"
                                >
                                    <Icone size={15} className="shrink-0 text-brass" aria-hidden="true" />
                                    {texto}
                                </span>
                            ))}
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
                        <p className="eyebrow">Drop atual</p>
                        <h2 className="mt-3 font-display text-2xl tracking-tight text-ink">
                            O que está na rua agora
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
                    <p className="eyebrow">Por que a gente faz isso</p>
                    <p className="mx-auto mt-6 max-w-2xl font-display text-2xl italic leading-snug text-ink">
                        Uma camiseta não converte ninguém. Ela só lembra, todo dia, de
                        quem você já é.
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
