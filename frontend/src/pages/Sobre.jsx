import { FiArrowRight } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import { STORE } from "@/data/store.js";

/**
 * Pagina de marca (`/sobre`).
 *
 * Conteudo editorial e ESTATICO de proposito: quem escreve o manifesto e o
 * marketing, nao o cadastro. Nao ha endpoint por tras desta tela.
 *
 * Regra de honestidade aplicada a copy: nada aqui afirma fato verificavel que a
 * loja nao nos deu — sem fundador, sem cidade, sem ano de fundacao, sem numero
 * de clientes. Todo texto fala de criterio e de metodo, que e o que a marca de
 * fato controla. Se um dia esses dados existirem, entram aqui com nome e data.
 *
 * Os blocos visuais sao os mesmos da Home: faixa `linen` alternando com `bone`,
 * filete como divisor e `font-display` nos titulos.
 */
const PILARES = [
    {
        indice: "01",
        titulo: "Estampa",
        texto: "Cada peça carrega uma ideia, não um clichê. A arte nasce da Palavra e vira gráfico de rua, nunca versículo por versículo sem propósito visual.",
    },
    {
        indice: "02",
        titulo: "Modelagem",
        texto: "Oversized de verdade: ombro caído, comprimento generoso, caimento que sobra sem parecer emprestado. Feita pra usar largada, do jeito que o streetwear pede.",
    },
    {
        indice: "03",
        titulo: "Produção",
        texto: "Lote curto e algodão pesado. Uma estampa só volta a ser produzida quando merece voltar. A coleção não cresce toda semana só pra ter novidade.",
    },
];

export default function Sobre() {
    return (
        <div className="animate-fade-up">
            {/* -------------------------------------------------------- ABERTURA */}
            <section className="border-b border-sand bg-linen">
                <div className="shell grid gap-10 py-16 lg:grid-cols-12 lg:py-24">
                    <div className="lg:col-span-5">
                        <p className="eyebrow">Sobre a marca</p>
                        <h1 className="mt-6 font-display text-3xl leading-[1.05] tracking-tight text-ink sm:text-4xl">
                            A arte de
                            <br />
                            <em className="font-normal italic text-olive">glorificar.</em>
                        </h1>
                    </div>

                    <div className="lg:col-span-7 lg:pt-4">
                        <p className="max-w-xl font-display text-xl italic leading-snug text-ink sm:text-2xl">
                            {STORE.manifesto}
                        </p>

                        <p className="mt-8 max-w-xl text-base leading-relaxed text-ink-soft">
                            A Glorific existe pra colocar fé em roupa que aguenta o dia a dia:
                            estampa com propósito, corte oversized, lote pequeno. Pregando sem
                            abrir a boca.
                        </p>
                    </div>
                </div>
            </section>

            {/* --------------------------------------------------------- PILARES */}
            <section className="shell py-16 lg:py-20">
                <p className="eyebrow">Como cada peça é decidida</p>

                <div className="mt-10 grid gap-10 sm:grid-cols-3">
                    {PILARES.map((pilar) => (
                        <article key={pilar.titulo}>
                            <div className="filete" />
                            <p className="mt-6 font-display text-sm tracking-tight text-taupe">
                                {pilar.indice}
                            </p>
                            <h2 className="mt-2 font-display text-xl tracking-tight text-ink">
                                {pilar.titulo}
                            </h2>
                            <p className="mt-3 text-sm leading-relaxed text-ink-soft">
                                {pilar.texto}
                            </p>
                        </article>
                    ))}
                </div>
            </section>

            {/* ----------------------------------------------------- FECHAMENTO */}
            <section className="border-b border-sand">
                <div className="shell py-20 text-center">
                    <h2 className="font-display text-2xl tracking-tight text-ink sm:text-3xl">
                        Comece pela vitrine
                    </h2>
                    <p className="mx-auto mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                        É lá que está tudo o que dá para levar hoje, com a medida de cada
                        tamanho aberta antes da compra.
                    </p>

                    <div className="mt-10 flex flex-wrap justify-center gap-4">
                        <Botao to="/catalogo" tamanho="lg">
                            Ver a coleção <FiArrowRight size={14} aria-hidden="true" />
                        </Botao>
                        <Botao to="/guia-de-medidas" variante="contorno" tamanho="lg">
                            Guia de medidas
                        </Botao>
                    </div>
                </div>
            </section>
        </div>
    );
}
