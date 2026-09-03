import { Link } from "react-router-dom";
import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { useColecoes } from "@/hooks/useCatalogo.js";

/**
 * Indice das colecoes VIGENTES.
 *
 * O backend so devolve o que esta no ar: drop agendado aparece sozinho na data
 * de inicio e sai sozinho na data de fim, sem ninguem publicar nada na mao.
 */
export default function Colecoes() {
    const { colecoes, isLoading, isError } = useColecoes();

    return (
        <div className="shell py-12 lg:py-16">
            <header className="max-w-2xl">
                <p className="eyebrow">Curadoria</p>
                <h1 className="mt-4 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Coleções
                </h1>
                <p className="mt-5 text-base leading-relaxed text-ink-soft">
                    Cada coleção nasce de uma ideia só, em lote curto. Quando acaba, acaba.
                </p>
            </header>

            <div className="mt-12">
                {isLoading ? (
                    <div className="grid gap-x-6 gap-y-12 sm:grid-cols-2 lg:grid-cols-3">
                        {Array.from({ length: 3 }).map((_, i) => (
                            <div key={i} className="flex flex-col gap-3">
                                <Skeleton className="aspect-[4/3] w-full" />
                                <Skeleton className="h-5 w-2/3" />
                                <Skeleton className="h-4 w-full" />
                            </div>
                        ))}
                    </div>
                ) : isError ? (
                    <p className="text-sm text-ink-soft">
                        Não foi possível carregar as coleções agora. Atualize a página para
                        tentar de novo.
                    </p>
                ) : colecoes.length === 0 ? (
                    <div className="border border-sand bg-linen px-6 py-16 text-center">
                        <p className="font-display text-xl tracking-tight text-ink">
                            Nenhuma coleção no ar neste momento.
                        </p>
                        <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                            A próxima estreia em breve. Enquanto isso, a vitrine continua aberta.
                        </p>
                        <Botao to="/catalogo" variante="contorno" className="mt-6">
                            Ver todas as peças
                        </Botao>
                    </div>
                ) : (
                    <div className="grid gap-x-6 gap-y-14 sm:grid-cols-2 lg:grid-cols-3">
                        {colecoes.map((colecao) => (
                            <article key={colecao.id} className="group">
                                <Link
                                    to={`/colecao/${colecao.slug}`}
                                    className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-4 focus-visible:ring-offset-base-100"
                                >
                                    <div className="aspect-[4/3] w-full overflow-hidden bg-linen">
                                        {colecao.urlMidiaCapa || colecao.urlMidiaBanner ? (
                                            <img
                                                src={colecao.urlMidiaCapa || colecao.urlMidiaBanner}
                                                alt={colecao.nome}
                                                loading="lazy"
                                                className="h-full w-full object-cover transition-transform duration-700 ease-out group-hover:scale-[1.03]"
                                            />
                                        ) : (
                                            <div
                                                aria-hidden="true"
                                                className="flex h-full w-full items-center justify-center bg-gradient-to-b from-sand via-linen to-bone"
                                            >
                                                <span className="font-display text-2xl text-ink/15">
                                                    ✦
                                                </span>
                                            </div>
                                        )}
                                    </div>

                                    <h2 className="mt-5 font-display text-xl tracking-tight text-ink">
                                        {colecao.nome}
                                    </h2>
                                </Link>

                                {colecao.epigrafe && (
                                    <p className="mt-2 font-display text-base italic leading-snug text-ink-soft">
                                        {colecao.epigrafe}
                                    </p>
                                )}

                                {colecao.descricao && (
                                    <p className="mt-3 text-sm leading-relaxed text-ink-soft">
                                        {colecao.descricao}
                                    </p>
                                )}
                            </article>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
