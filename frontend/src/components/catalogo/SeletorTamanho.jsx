/**
 * Seletor de tamanho da pagina de produto.
 *
 * Duas regras que valem mais que o visual:
 *  1. a ordem e a `Ordem` que o backend manda (P, M, G, GG) — nunca alfabetica;
 *  2. tamanho sem saldo aparece DESABILITADO e riscado, jamais escondido.
 *     Esconder faz a pessoa achar que a peca nunca teve aquele numero; mostrar
 *     esgotado e o que traz ela de volta quando repor.
 */
export default function SeletorTamanho({
    opcoes = [],
    idSelecionado = null,
    onSelecionar,
    erro = null,
    estoqueBaixoAte = 3,
}) {
    if (!opcoes.length) {
        return (
            <p className="text-sm text-ink-soft">
                Esta peça ainda não tem grade de tamanhos publicada.
            </p>
        );
    }

    const selecionada = opcoes.find((o) => o.id === idSelecionado) ?? null;
    const poucasUnidades =
        selecionada?.disponivel &&
        selecionada.quantidadeDisponivel > 0 &&
        selecionada.quantidadeDisponivel <= estoqueBaixoAte;

    return (
        <div>
            <div
                role="group"
                aria-label="Tamanho"
                className="flex flex-wrap gap-2"
            >
                {opcoes.map((opcao) => {
                    const selecionado = opcao.id === idSelecionado;
                    const bloqueado = !opcao.disponivel;

                    const motivo = !opcao.existe
                        ? "Não sai nesta cor"
                        : bloqueado
                          ? "Esgotado"
                          : null;

                    return (
                        <button
                            key={opcao.id}
                            type="button"
                            disabled={bloqueado}
                            aria-pressed={selecionado}
                            aria-label={motivo ? `${opcao.codigo}, ${motivo}` : opcao.codigo}
                            title={motivo ?? undefined}
                            onClick={() => onSelecionar?.(opcao)}
                            className={[
                                "relative inline-flex h-11 min-w-[3rem] items-center justify-center",
                                "rounded-none border px-3 font-sans text-xs uppercase tracking-widest",
                                "transition-colors duration-200",
                                selecionado
                                    ? "border-ink bg-ink text-bone"
                                    : "border-sand bg-base-100 text-ink hover:border-ink",
                                bloqueado
                                    ? "cursor-not-allowed border-sand bg-linen text-taupe hover:border-sand"
                                    : "",
                            ].join(" ")}
                        >
                            {opcao.codigo}
                            {bloqueado && (
                                <span
                                    aria-hidden="true"
                                    className="absolute left-1 right-1 top-1/2 h-px -rotate-12 bg-taupe"
                                />
                            )}
                        </button>
                    );
                })}
            </div>

            {erro && (
                <p role="alert" className="mt-3 text-xs text-danger">
                    {erro}
                </p>
            )}

            {!erro && poucasUnidades && (
                <p className="mt-3 text-xs text-clay">
                    {selecionada.quantidadeDisponivel === 1
                        ? `Última peça no tamanho ${selecionada.codigo}.`
                        : `Últimas ${selecionada.quantidadeDisponivel} peças no tamanho ${selecionada.codigo}.`}
                </p>
            )}
        </div>
    );
}
