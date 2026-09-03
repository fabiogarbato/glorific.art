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
 * loja nao nos deu — sem fundadora, sem cidade, sem ano de fundacao, sem numero
 * de clientes. Todo texto fala de criterio e de metodo, que e o que a marca de
 * fato controla. Se um dia esses dados existirem, entram aqui com nome e data.
 *
 * Os blocos visuais sao os mesmos da Home: faixa `linen` alternando com `bone`,
 * filete como divisor e `font-display` nos titulos.
 */
const PILARES = [
    {
        indice: "01",
        titulo: "Tecido",
        texto: "Fibra natural sempre que a peça permite: linho, algodão e viscose de origem responsável. O tecido é escolhido pelo caimento depois de lavado, não pelo brilho na arara.",
    },
    {
        indice: "02",
        titulo: "Modelagem",
        texto: "Cobertura sem volume. Comprimento, cava e ombro são medidos no corpo em movimento — sentada, de braço erguido, com criança no colo — antes de virarem grade de tamanho.",
    },
    {
        indice: "03",
        titulo: "Produção",
        texto: "Lote curto e costura nacional. Uma peça só volta a ser produzida quando merece voltar, e é por isso que a coleção não cresce todo mês.",
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
                            Roupa que
                            <br />
                            <em className="font-normal italic text-olive">acompanha</em>
                            <br />
                            a vida inteira.
                        </h1>
                    </div>

                    <div className="lg:col-span-7 lg:pt-4">
                        <p className="max-w-xl font-display text-xl italic leading-snug text-ink sm:text-2xl">
                            {STORE.manifesto}
                        </p>

                        <div className="mt-8 max-w-xl space-y-5 text-base leading-relaxed text-ink-soft">
                            <p>
                                A glorific.art nasceu de um incômodo simples: quem procura uma
                                roupa mais coberta costuma ter que escolher entre parecer
                                fantasiada e parecer desleixada. Nenhuma das duas coisas tem a
                                ver com fé.
                            </p>
                            <p>
                                Nossa resposta é sóbria. Cor fechada, corte limpo, acabamento
                                que aguenta o uso de segunda a segunda. A peça não anuncia nada
                                por você — ela só não atrapalha o que você tem para dizer.
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
                            O sagrado aparece pela sobriedade, não pela estampa.
                        </p>
                    </div>

                    <div className="lg:col-span-8">
                        <div className="max-w-2xl space-y-5 text-base leading-relaxed text-ink-soft">
                            <p>
                                Moda cristã, para nós, não é um tema impresso na frente da
                                camiseta. É o cuidado com quem faz, com o que se compra e com o
                                tempo que a peça vai durar no armário. Comprar menos e usar por
                                mais tempo também é uma forma de respeito.
                            </p>
                            <p>
                                Por isso preferimos a coleção curta ao lançamento semanal, e
                                preferimos dizer que uma peça acabou a repor às pressas com um
                                tecido pior. Quando escrevemos o guia de medidas com detalhe, é
                                pelo mesmo motivo: a roupa que serve na primeira tentativa é a
                                que menos volta pelo correio e a que mais tempo fica com você.
                            </p>
                            <p>
                                Não prometemos perfeição. Prometemos ficha técnica honesta,
                                medida conferida na régua e uma resposta de gente quando algo
                                sair errado.
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
                        É lá que está tudo o que dá para levar hoje — com a medida de cada
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
