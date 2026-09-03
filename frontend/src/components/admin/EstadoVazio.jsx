/**
 * Estado vazio com texto útil: diz por que a lista está vazia e qual é o
 * próximo passo. Nunca um "sem registros" seco.
 */
export default function EstadoVazio({ titulo, mensagem, acao, className = "" }) {
    return (
        <div
            className={`border border-dashed border-sand bg-linen/40 px-6 py-16 text-center ${className}`}
        >
            <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>
            {mensagem && (
                <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                    {mensagem}
                </p>
            )}
            {acao && <div className="mt-6 flex justify-center">{acao}</div>}
        </div>
    );
}
