/**
 * Moldura das telas de sessao (entrar, criar cadastro, recuperar e redefinir).
 *
 * Coluna estreita e muito respiro: formulario de sessao nao compete com
 * vitrine, ele so precisa ser lido rapido e sem duvida de onde clicar.
 *
 * `erro` existe porque 401 e 403 nao viram toast — a mensagem de credencial
 * errada tem de aparecer ao lado do formulario que a causou, e nao num aviso
 * flutuante no canto da tela.
 */
export default function MolduraAuth({ rotulo, titulo, descricao, erro, children, rodape }) {
    return (
        <div className="shell flex justify-center py-16 lg:py-24">
            <div className="w-full max-w-sm">
                {rotulo && <p className="eyebrow">{rotulo}</p>}

                <h1 className="mt-4 font-display text-2xl tracking-tight text-ink">{titulo}</h1>

                {descricao && (
                    <p className="mt-3 text-sm leading-relaxed text-ink-soft">{descricao}</p>
                )}

                {erro && (
                    <p
                        role="alert"
                        className="mt-6 border-l-2 border-danger bg-linen px-4 py-3 text-sm leading-relaxed text-ink"
                    >
                        {erro}
                    </p>
                )}

                {children}

                {rodape && <div className="mt-8 flex flex-col gap-3 text-sm">{rodape}</div>}
            </div>
        </div>
    );
}
