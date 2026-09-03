import { FiAlertCircle, FiInbox } from "react-icons/fi";
import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

/**
 * Os tres estados que toda tela do painel precisa ter: carregando, erro e
 * vazio. Ficam juntos num arquivo so porque nunca aparecem sozinhos — quem
 * importa um quase sempre importa os outros dois.
 *
 * O texto de vazio e sempre UTIL: diz por que a lista esta vazia e qual e o
 * proximo passo. "Nenhum registro" nao ajuda ninguem.
 */

export function CabecalhoPagina({ sobretitulo, titulo, descricao, acoes }) {
    return (
        <header className="mb-8 flex flex-wrap items-end justify-between gap-4">
            <div className="min-w-0">
                {sobretitulo && <p className="eyebrow">{sobretitulo}</p>}
                <h1 className="mt-3 font-display text-2xl tracking-tight text-ink">{titulo}</h1>
                {descricao && (
                    <p className="mt-2 max-w-2xl text-sm leading-relaxed text-ink-soft">
                        {descricao}
                    </p>
                )}
            </div>
            {acoes && <div className="flex flex-wrap items-center gap-2">{acoes}</div>}
        </header>
    );
}

export function EstadoErro({
    titulo = "Não foi possível carregar",
    mensagem = "A consulta falhou. Verifique a conexão e tente novamente.",
    onTentarDeNovo,
}) {
    return (
        <div
            role="alert"
            className="flex flex-col items-center gap-3 border border-danger/40 bg-linen px-6 py-14 text-center"
        >
            <FiAlertCircle size={22} className="text-danger" aria-hidden="true" />
            <p className="font-display text-xl tracking-tight text-ink">{titulo}</p>
            <p className="max-w-md text-sm leading-relaxed text-ink-soft">{mensagem}</p>
            {onTentarDeNovo && (
                <Botao variante="contorno" tamanho="sm" onClick={onTentarDeNovo} className="mt-2">
                    Tentar de novo
                </Botao>
            )}
        </div>
    );
}

export function EstadoVazio({ titulo = "Nada por aqui", mensagem, acao, Icone = FiInbox }) {
    return (
        <div className="flex flex-col items-center gap-3 border border-sand bg-linen px-6 py-16 text-center">
            <Icone size={22} className="text-taupe" aria-hidden="true" />
            <p className="font-display text-xl tracking-tight text-ink">{titulo}</p>
            {mensagem && (
                <p className="max-w-md text-sm leading-relaxed text-ink-soft">{mensagem}</p>
            )}
            {acao && <div className="mt-2">{acao}</div>}
        </div>
    );
}

/** Bloco de conteudo do painel: filete no topo, titulo serifado e respiro. */
export function BlocoSecao({ titulo, descricao, acoes, children, className = "" }) {
    return (
        <section className={`mb-12 ${className}`}>
            {(titulo || acoes) && (
                <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
                    <div className="min-w-0">
                        {titulo && (
                            <h2 className="font-display text-xl tracking-tight text-ink">
                                {titulo}
                            </h2>
                        )}
                        {descricao && <p className="mt-1 text-sm text-ink-soft">{descricao}</p>}
                    </div>
                    {acoes && <div className="flex flex-wrap items-center gap-2">{acoes}</div>}
                </div>
            )}
            {children}
        </section>
    );
}

/** Esqueleto de painel de cartões, para o primeiro carregamento. */
export function SkeletonCartoes({ quantidade = 4 }) {
    return (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            {Array.from({ length: quantidade }).map((_, i) => (
                <div key={i} className="border border-sand bg-linen px-4 py-5">
                    <Skeleton className="h-3 w-24" />
                    <Skeleton className="mt-4 h-7 w-32" />
                </div>
            ))}
        </div>
    );
}

export default EstadoVazio;
