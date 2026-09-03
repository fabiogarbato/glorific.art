import { Link } from "react-router-dom";
import Badge from "@/components/ui/Badge.jsx";
import SwatchCor from "./SwatchCor.jsx";
import EstrelasNota from "./EstrelasNota.jsx";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Card da vitrine.
 *
 * Sem borda e sem sombra por decisao de marca: o respiro do grid separa os
 * cards. A foto e 3:4 (retrato), nunca quadrada — foto de moda mostra o corpo
 * inteiro da peca.
 *
 * Recebe um `ProdutoCardDto`: nome, slug, precoAPartirDeCentavos,
 * precoComparativoCentavos, urlImagemCapa, cores[], esgotado, notaMedia.
 */
const MAX_SWATCHES = 5;

export default function CardProduto({ produto, carregamentoAntecipado = false }) {
    if (!produto) return null;

    const {
        nome,
        slug,
        urlImagemCapa,
        altImagemCapa,
        precoAPartirDeCentavos,
        precoComparativoCentavos,
        nomeCategoria,
        cores = [],
        esgotado,
        notaMedia,
        totalAvaliacoes,
    } = produto;

    const emOferta =
        !!precoComparativoCentavos && precoComparativoCentavos > precoAPartirDeCentavos;

    const swatches = cores.slice(0, MAX_SWATCHES);
    const restantes = cores.length - swatches.length;

    return (
        <article className="group flex flex-col">
            <Link
                to={`/produto/${slug}`}
                className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-4 focus-visible:ring-offset-base-100"
            >
                <div className="relative w-full overflow-hidden bg-linen">
                    <div className="aspect-product w-full">
                        {urlImagemCapa ? (
                            <img
                                src={urlImagemCapa}
                                alt={altImagemCapa || nome}
                                loading={carregamentoAntecipado ? "eager" : "lazy"}
                                decoding="async"
                                className="h-full w-full object-cover transition-transform duration-700 ease-out group-hover:scale-[1.03]"
                            />
                        ) : (
                            <div
                                aria-hidden="true"
                                className="flex h-full w-full items-center justify-center bg-gradient-to-b from-sand via-linen to-bone"
                            >
                                <span className="font-display text-2xl text-ink/15">✦</span>
                            </div>
                        )}
                    </div>

                    {(esgotado || emOferta) && (
                        <div className="absolute left-3 top-3 flex flex-col items-start gap-2">
                            {esgotado && <Badge variante="esgotado">Esgotado</Badge>}
                            {!esgotado && emOferta && <Badge variante="promocao">Oferta</Badge>}
                        </div>
                    )}
                </div>

                <h3 className="mt-4 font-sans text-sm font-medium leading-snug text-ink">
                    {nome}
                </h3>
            </Link>

            {nomeCategoria && (
                <p className="mt-1 text-xs uppercase tracking-widest text-taupe">
                    {nomeCategoria}
                </p>
            )}

            <p className="preco mt-2 flex flex-wrap items-baseline gap-2 text-sm text-ink">
                {emOferta && (
                    <span className="text-xs text-taupe line-through">
                        {formatarCentavosParaBRL(precoComparativoCentavos)}
                    </span>
                )}
                <span className={emOferta ? "text-clay" : undefined}>
                    {formatarCentavosParaBRL(precoAPartirDeCentavos)}
                </span>
            </p>

            {(swatches.length > 0 || totalAvaliacoes > 0) && (
                <div className="mt-3 flex items-center justify-between gap-3">
                    {swatches.length > 0 ? (
                        <span className="flex items-center gap-1.5">
                            {swatches.map((cor) => (
                                <SwatchCor key={cor.id} cor={cor} tamanho="sm" />
                            ))}
                            {restantes > 0 && (
                                <span className="text-xs text-taupe">+{restantes}</span>
                            )}
                        </span>
                    ) : (
                        <span />
                    )}

                    {totalAvaliacoes > 0 && (
                        <EstrelasNota nota={notaMedia} total={totalAvaliacoes} tamanho={12} />
                    )}
                </div>
            )}
        </article>
    );
}
