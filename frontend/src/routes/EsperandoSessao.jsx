import Skeleton from "@/components/ui/Skeleton.jsx";

/**
 * Espera enquanto a sessao e restaurada (o refresh silencioso da montagem).
 *
 * E um esqueleto, e nao um spinner centralizado: a janela e curta e um giro no
 * meio da tela chama mais atencao do que o proprio conteudo que vai chegar.
 * `aria-busy` e o texto em `sr-only` contam o que esta acontecendo para quem
 * usa leitor de tela — a animacao sozinha nao conta nada.
 */
export default function EsperandoSessao() {
    return (
        <div className="shell py-16 lg:py-24" aria-busy="true">
            <span className="sr-only">Verificando sua sessão…</span>
            <Skeleton className="h-3 w-28" />
            <Skeleton className="mt-6 h-8 w-64" />
            <div className="mt-10 flex flex-col gap-3">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-5/6" />
                <Skeleton className="h-4 w-2/3" />
            </div>
        </div>
    );
}
