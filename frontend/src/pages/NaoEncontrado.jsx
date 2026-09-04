import Botao from "@/components/ui/Botao.jsx";

export default function NaoEncontrado() {
    return (
        <div className="shell flex min-h-[60vh] flex-col items-center justify-center py-20 text-center">
            <p className="eyebrow">Erro 404</p>

            <h1 className="mt-6 font-display text-3xl tracking-tight text-ink sm:text-4xl">
                Página não encontrada
            </h1>

            <p className="mt-6 max-w-md text-base leading-relaxed text-ink-soft">
                O endereço que você acessou não existe mais, ou nunca existiu. Talvez a peça
                que procurava tenha saído de catálogo.
            </p>

            <div className="mt-10 flex flex-wrap justify-center gap-4">
                <Botao to="/">Voltar ao início</Botao>
                <Botao to="/catalogo" variante="contorno">
                    Ver a coleção
                </Botao>
            </div>
        </div>
    );
}
