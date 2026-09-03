import Skeleton from "@/components/ui/Skeleton.jsx";

/**
 * Faixa de indicadores do topo da tela.
 *
 * Cada item: `{ rotulo, valor, Icone?, alerta? }`. O `alerta` pinta só o ícone
 * de `warning` — o número continua em `ink`, para a faixa não virar semáforo.
 */
export default function FaixaKpis({ itens = [], carregando = false, className = "" }) {
    if (!carregando && itens.length === 0) return null;

    return (
        <section
            aria-label="Indicadores"
            className={`mb-10 grid grid-cols-2 gap-3 lg:grid-cols-4 ${className}`}
        >
            {carregando
                ? Array.from({ length: 4 }).map((_, i) => (
                      <article key={`kpi-${i}`} className="border border-sand bg-linen px-4 py-5">
                          <Skeleton className="h-3 w-24" />
                          <Skeleton className="mt-4 h-6 w-16" />
                      </article>
                  ))
                : itens.map(({ rotulo, valor, Icone, alerta, ajuda }) => (
                      <article key={rotulo} className="border border-sand bg-linen px-4 py-5">
                          <div className="flex items-center gap-2">
                              {Icone && (
                                  <Icone
                                      size={14}
                                      className={alerta ? "text-warning" : "text-ink-soft"}
                                      aria-hidden="true"
                                  />
                              )}
                              <span className="text-xs uppercase tracking-widest text-ink-soft">
                                  {rotulo}
                              </span>
                          </div>
                          <p className="preco mt-3 font-display text-xl text-ink">{valor}</p>
                          {ajuda && <p className="mt-1 text-xs text-taupe">{ajuda}</p>}
                      </article>
                  ))}
        </section>
    );
}
