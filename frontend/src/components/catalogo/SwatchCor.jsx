/**
 * Bolinha de cor. Botao quando ha `onSelecionar`; enfeite (no card da vitrine)
 * quando nao ha.
 *
 * A cor chapada vem do `hexRgb` do backend; estampa tem `urlSwatch` e entra
 * como imagem de fundo, porque xadrez e floral nao cabem num hex.
 */
const DIMENSOES = {
    sm: "h-4 w-4",
    md: "h-7 w-7",
    lg: "h-9 w-9",
};

function estiloDaCor(cor) {
    if (cor?.urlSwatch) {
        return {
            backgroundImage: `url(${cor.urlSwatch})`,
            backgroundSize: "cover",
            backgroundPosition: "center",
        };
    }
    return { backgroundColor: cor?.hexRgb || "var(--sand)" };
}

export default function SwatchCor({
    cor,
    selecionada = false,
    onSelecionar,
    tamanho = "md",
    indisponivel = false,
}) {
    const dimensao = DIMENSOES[tamanho] ?? DIMENSOES.md;

    // Anel interno claro separa a bolinha do off-white sem virar borda grossa.
    const base = `relative inline-block rounded-full ${dimensao} shadow-[inset_0_0_0_1px_rgba(28,26,23,0.12)]`;

    if (!onSelecionar) {
        return <span className={base} style={estiloDaCor(cor)} title={cor?.nome} />;
    }

    return (
        <button
            type="button"
            onClick={() => onSelecionar(cor)}
            aria-pressed={selecionada}
            aria-label={indisponivel ? `${cor?.nome} — sem peças disponíveis` : cor?.nome}
            title={cor?.nome}
            className={[
                base,
                "transition-transform duration-200",
                "ring-offset-2 ring-offset-base-100",
                selecionada ? "ring-2 ring-ink" : "ring-1 ring-sand hover:ring-taupe",
                indisponivel ? "opacity-45" : "",
            ].join(" ")}
        >
            {indisponivel && (
                <span
                    aria-hidden="true"
                    className="absolute left-1/2 top-1/2 h-px w-full -translate-x-1/2 -translate-y-1/2 rotate-45 bg-ink/50"
                />
            )}
        </button>
    );
}
