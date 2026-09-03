/**
 * Cinco estrelas com preenchimento proporcional (4,3 preenche 86% da faixa).
 *
 * Duas camadas sobrepostas em vez de "meia estrela": a camada cheia e recortada
 * por largura, entao qualquer decimal aparece certo sem arredondar a nota do
 * cliente para cima.
 */
import { formatarNota } from "@/lib/vitrine.js";

const CAMINHO_ESTRELA =
    "M12 2.4l2.86 6.02 6.44.9-4.68 4.6 1.13 6.5L12 17.3l-5.75 3.12 1.13-6.5-4.68-4.6 6.44-.9z";

function Fileira({ cor, tamanho }) {
    return (
        <div className="flex" aria-hidden="true">
            {[0, 1, 2, 3, 4].map((i) => (
                <svg
                    key={i}
                    width={tamanho}
                    height={tamanho}
                    viewBox="0 0 24 24"
                    fill={cor}
                    className="shrink-0"
                >
                    <path d={CAMINHO_ESTRELA} />
                </svg>
            ))}
        </div>
    );
}

export default function EstrelasNota({
    nota,
    total = null,
    tamanho = 14,
    mostrarNumero = false,
    className = "",
}) {
    const valor = Number(nota);
    const temNota = Number.isFinite(valor) && valor > 0;
    const percentual = temNota ? Math.min(100, Math.max(0, (valor / 5) * 100)) : 0;
    const formatada = formatarNota(valor);

    const descricao = temNota
        ? `Nota ${formatada} de 5${total ? ` em ${total} avaliações` : ""}`
        : "Ainda sem avaliações";

    return (
        <span
            className={`inline-flex items-center gap-2 ${className}`}
            role="img"
            aria-label={descricao}
        >
            <span className="relative inline-block leading-none">
                <Fileira cor="var(--sand)" tamanho={tamanho} />
                <span
                    className="absolute inset-y-0 left-0 overflow-hidden"
                    style={{ width: `${percentual}%` }}
                >
                    <Fileira cor="var(--brass)" tamanho={tamanho} />
                </span>
            </span>

            {mostrarNumero && (
                <span className="preco text-xs text-ink-soft">
                    {temNota ? formatada : "—"}
                    {total ? ` (${total})` : ""}
                </span>
            )}
        </span>
    );
}
