/**
 * Cabecalho de toda tela do painel: sobrancelha, título serifado, uma linha de
 * contexto e o bloco de ações à direita.
 *
 * O respiro é o mesmo do resto do sistema visual — o admin tem densidade maior
 * que a loja, mas o topo da página continua editorial.
 */
export default function CabecalhoPagina({
    sobrancelha = "Painel",
    titulo,
    descricao,
    acoes,
    className = "",
}) {
    return (
        <header
            className={`mb-8 flex flex-col gap-4 border-b border-sand pb-6 sm:flex-row sm:items-end sm:justify-between ${className}`}
        >
            <div className="min-w-0">
                {sobrancelha && <p className="eyebrow">{sobrancelha}</p>}
                <h1 className="mt-3 font-display text-2xl tracking-tight text-ink">{titulo}</h1>
                {descricao && (
                    <p className="mt-2 max-w-2xl text-sm leading-relaxed text-ink-soft">
                        {descricao}
                    </p>
                )}
            </div>

            {acoes && <div className="flex shrink-0 flex-wrap items-center gap-2">{acoes}</div>}
        </header>
    );
}
