/**
 * A mesma linha da tabela, no formato de card — é o que o painel mostra abaixo
 * de `sm`, onde uma tabela de sete colunas vira rolagem horizontal inútil.
 *
 * Contrato: `campos = [{ rotulo, valor }]`. A página monta a tabela do desktop
 * e este card com os MESMOS dados, para as duas leituras não divergirem.
 *
 * O card inteiro nunca é um `<button>`: ele carrega os botões de ação, e botão
 * dentro de botão é HTML inválido (e o teclado perde o alvo). Quando há
 * `onClick`, quem vira controle é só o título.
 */
export default function LinhaTabela({
    titulo,
    subtitulo,
    selo,
    campos = [],
    acoes,
    onClick,
    className = "",
}) {
    return (
        <div className={`border border-sand bg-base-100 px-4 py-4 ${className}`}>
            <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                    {onClick ? (
                        <button
                            type="button"
                            onClick={onClick}
                            className="max-w-full truncate text-left font-display text-lg leading-tight text-ink underline-offset-4 hover:underline"
                        >
                            {titulo}
                        </button>
                    ) : (
                        <p className="truncate font-display text-lg leading-tight text-ink">
                            {titulo}
                        </p>
                    )}

                    {subtitulo && (
                        <p className="mt-1 truncate font-sans text-xs text-ink-soft">{subtitulo}</p>
                    )}
                </div>

                {selo && <div className="shrink-0">{selo}</div>}
            </div>

            {campos.length > 0 && (
                <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3">
                    {campos.map(({ rotulo, valor }) => (
                        <div key={rotulo} className="min-w-0">
                            <dt className="text-xs uppercase tracking-widest text-taupe">
                                {rotulo}
                            </dt>
                            <dd className="mt-1 truncate font-sans text-sm text-ink">{valor}</dd>
                        </div>
                    ))}
                </dl>
            )}

            {acoes && (
                <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-sand pt-3">
                    {acoes}
                </div>
            )}
        </div>
    );
}
