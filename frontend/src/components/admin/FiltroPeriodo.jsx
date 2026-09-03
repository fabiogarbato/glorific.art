import Campo from "@/components/ui/Campo.jsx";
import { PRESETS_PERIODO } from "@/lib/periodo.js";

/**
 * Seletor de período do painel: atalhos + intervalo manual.
 *
 * Os dois campos de data só aparecem no modo personalizado — deixá-los sempre
 * visíveis convida o operador a mexer neles sem perceber que o atalho ao lado
 * continua mandando.
 *
 * Contrato: `valor` é `{ preset, de, ate }` com `de`/`ate` no formato
 * "aaaa-mm-dd" (o mesmo do `<input type="date">`).
 */
export default function FiltroPeriodo({ valor, onChange, className = "" }) {
    const { preset, de, ate } = valor;

    const trocarPreset = (novo) => onChange({ ...valor, preset: novo });

    return (
        <div className={`flex flex-wrap items-end gap-3 ${className}`}>
            <div
                role="group"
                aria-label="Período do relatório"
                className="flex flex-wrap items-center gap-0 border border-sand"
            >
                {PRESETS_PERIODO.map((p) => (
                    <button
                        key={p.chave}
                        type="button"
                        aria-pressed={preset === p.chave}
                        onClick={() => trocarPreset(p.chave)}
                        className={`h-9 px-3 font-sans text-[11px] uppercase tracking-widest transition-colors ${
                            preset === p.chave
                                ? "bg-olive text-bone"
                                : "bg-base-100 text-ink-soft hover:bg-linen hover:text-ink"
                        }`}
                    >
                        {p.rotulo}
                    </button>
                ))}
            </div>

            {preset === "personalizado" && (
                <div className="flex flex-wrap items-end gap-3">
                    <Campo
                        label="De"
                        type="date"
                        value={de ?? ""}
                        max={ate || undefined}
                        onChange={(e) => onChange({ ...valor, de: e.target.value })}
                        containerClassName="w-40"
                    />
                    <Campo
                        label="Até"
                        type="date"
                        value={ate ?? ""}
                        min={de || undefined}
                        onChange={(e) => onChange({ ...valor, ate: e.target.value })}
                        containerClassName="w-40"
                    />
                </div>
            )}
        </div>
    );
}
