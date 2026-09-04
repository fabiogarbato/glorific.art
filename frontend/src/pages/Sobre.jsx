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

                        <div className="mt-8 max-w-xl space-y-5 text-base leading-relaxed text-ink-soft">
                            <p>
                                Antes de virar coleção, a Glorific foi um propósito: glorificar
                                a Ele. A gente ama a Deus, e quis que isso aparecesse em roupa
                                antes de aparecer em discurso. Anunciar Cristo não seria só
                                palavra: seria corte, tecido, prazo cumprido, estampa que
                                aguenta o uso do dia a dia. Pregando sem abrir a boca, do jeito
                                que a roupa sozinha consegue.
                            </p>
                            <p>
                                A Glorific nasceu de um incômodo simples: fé e estética de rua
                                quase nunca dividem o mesmo cabide. De um lado, a camiseta
                                gospel de estampa apressada. Do outro, o streetwear que não
                                tem nada pra dizer. A gente não quis escolher.
                            </p>
                            <p>
                                Cada peça é oversized, pesada, feita pra durar, e carrega uma
                                estampa que é, ao mesmo tempo, arte de rua e confissão de fé.
                                Glorificar não é um tema estampado na frente. É o cuidado com
                                o traço, com o tecido e com quem vai vestir isso todo dia.
                            </p>
                        </div>
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

            {/* -------------------------------------------------------- PROPOSITO */}
            <section className="border-y border-sand bg-linen">
                <div className="shell grid gap-10 py-16 lg:grid-cols-12 lg:py-20">
                    <div className="lg:col-span-4">
                        <p className="eyebrow">O propósito</p>
                        <p className="mt-6 font-display text-2xl italic leading-snug text-ink">
                            O sagrado aparece pela estampa, não pela sobriedade.
                        </p>
                    </div>

                    <div className="lg:col-span-8">
                        <div className="max-w-2xl space-y-5 text-base leading-relaxed text-ink-soft">
                            <p>
                                Moda cristã, pra nós, não é evitar chamar atenção. É chamar
                                atenção pro que importa. Uma camiseta que alguém pergunta "onde
                                você comprou" é uma porta aberta pra falar de fé sem precisar
                                dizer uma palavra antes.
                            </p>
                            <p>
                                Por isso preferimos a coleção curta ao lançamento toda semana,
                                e preferimos dizer que uma estampa esgotou a repor às pressas
                                com um algodão fino. Oversized não é só estética: é a peça
                                sobrando no corpo pra caber o resto: o testemunho, o incômodo
                                bom, a pergunta que alguém vai fazer no elevador.
                            </p>
                            <p>
                                Não prometemos perfeição. Prometemos ficha técnica honesta,
                                estampa que não racha na segunda lavagem e uma resposta de
                                gente quando algo sair errado.
                            </p>
                        </div>
                    </div>
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
