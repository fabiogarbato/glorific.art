/**
 * Placeholder de carga na cor `sand` — o mesmo cinza-areia das bordas, para o
 * esqueleto nao gritar mais que o conteudo.
 */
export default function Skeleton({ className = "h-4 w-full" }) {
    return <div aria-hidden="true" className={`animate-pulse bg-sand/70 ${className}`} />;
}

/** Esqueleto de card de vitrine: foto 3:4 + nome + preco. */
export function SkeletonCard() {
    return (
        <div className="flex flex-col gap-3">
            <Skeleton className="aspect-product w-full" />
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/3" />
        </div>
    );
}

/** N linhas de texto, a ultima mais curta (imita paragrafo real). */
export function SkeletonTexto({ linhas = 3 }) {
    return (
        <div className="flex flex-col gap-2">
            {Array.from({ length: linhas }).map((_, i) => (
                <Skeleton key={i} className={`h-3.5 ${i === linhas - 1 ? "w-2/3" : "w-full"}`} />
            ))}
        </div>
    );
}
