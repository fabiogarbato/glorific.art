import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Resumo de valores. Puramente de exibição: NÃO calcula nada além de somar
 * inteiros já vindos do servidor (subtotal − desconto + frete), tudo em
 * centavos. Nenhuma conta com float, nenhuma regra de desconto reimplementada
 * aqui — a autoridade sobre dinheiro é o backend.
 *
 * `freteCentavos = null` significa "ainda não calculado", que é diferente de
 * frete zero (cortesia). Misturar os dois faria o total aparecer menor do que é.
 */
function Linha({ rotulo, valor, destaque = false, className = "" }) {
    return (
        <div className={`flex items-baseline justify-between gap-4 ${className}`}>
            <dt className={destaque ? "font-sans text-sm text-ink" : "text-sm text-ink-soft"}>
                {rotulo}
            </dt>
            <dd className={`preco ${destaque ? "text-base text-ink" : "text-sm text-ink"}`}>
                {valor}
            </dd>
        </div>
    );
}

export default function ResumoPedido({
    subtotalCentavos = 0,
    descontoCentavos = 0,
    freteCentavos = null,
    freteGratis = false,
    codigoCupom = null,
    quantidadeItens = 0,
    titulo = "Resumo",
    children,
}) {
    const freteCalculado = freteCentavos != null;
    const totalCentavos = subtotalCentavos - descontoCentavos + (freteCentavos ?? 0);

    return (
        <section
            aria-label={titulo}
            className="flex flex-col gap-5 border border-sand bg-linen px-5 py-6 sm:px-6"
        >
            <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>

            <dl className="flex flex-col gap-3">
                <Linha
                    rotulo={`Subtotal${quantidadeItens ? ` · ${quantidadeItens} ${quantidadeItens === 1 ? "peça" : "peças"}` : ""}`}
                    valor={formatarCentavosParaBRL(subtotalCentavos)}
                />

                {descontoCentavos > 0 && (
                    <Linha
                        rotulo={codigoCupom ? `Desconto · ${codigoCupom}` : "Desconto"}
                        valor={`− ${formatarCentavosParaBRL(descontoCentavos)}`}
                        className="text-olive"
                    />
                )}

                <Linha
                    rotulo="Frete"
                    valor={
                        !freteCalculado
                            ? "A calcular"
                            : freteGratis || freteCentavos === 0
                              ? "Cortesia"
                              : formatarCentavosParaBRL(freteCentavos)
                    }
                />

                <div className="filete my-1" />

                <Linha
                    rotulo="Total"
                    destaque
                    valor={
                        freteCalculado
                            ? formatarCentavosParaBRL(totalCentavos)
                            : `${formatarCentavosParaBRL(totalCentavos)} + frete`
                    }
                />
            </dl>

            {children}
        </section>
    );
}
