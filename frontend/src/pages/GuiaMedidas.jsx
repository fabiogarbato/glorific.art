import { useState } from "react";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { useTabelasMedidas } from "@/hooks/useMedidas.js";

/**
 * Guia de medidas da loja (`/guia-de-medidas`).
 *
 * Tela de DADO, nao de texto: as tabelas vem de `GET /api/v1/tabelas-medidas`
 * (publico) pela cadeia page -> hook -> service -> client. Escrever a grade na
 * mao aqui garantiria, em algumas semanas, um guia divergente do que o admin
 * cadastrou — e medida errada vira devolucao.
 *
 * Decisoes da tela:
 *  - as cinco colunas de medida ficam SEMPRE visiveis, com travessao onde a
 *    medida nao foi cadastrada. Numa pagina de referencia (diferente do modal
 *    do produto, que mostra so o que aquela peca tem) sumir com a coluna faria
 *    tabelas vizinhas parecerem ter o mesmo cabecalho com sentidos diferentes;
 *  - o scroll horizontal e do BLOCO da tabela, nunca da pagina: no celular o
 *    corpo do site continua rolando so na vertical;
 *  - vazio sem erro e estado honesto ("ainda nao cadastrou"), e nao uma tela
 *    branca nem uma mensagem de falha.
 */
const COLUNAS = [
    { chave: "bustoCm", rotulo: "Busto" },
    { chave: "cinturaCm", rotulo: "Cintura" },
    { chave: "quadrilCm", rotulo: "Quadril" },
    { chave: "comprimentoCm", rotulo: "Comprimento" },
    { chave: "mangaCm", rotulo: "Manga" },
];

/** Como tirar cada medida. Este bloco e o que mais derruba troca por tamanho. */
const COMO_MEDIR = [
    {
        titulo: "Busto",
        texto: "Passe a fita pela parte mais cheia do busto, mantendo-a na horizontal também nas costas.",
    },
    {
        titulo: "Cintura",
        texto: "Meça na parte mais estreita do tronco, geralmente logo acima do umbigo. Respire normalmente.",
    },
    {
        titulo: "Quadril",
        texto: "Contorne a parte mais larga do quadril, com os pés juntos.",
    },
    {
        titulo: "Comprimento",
        texto: "Do ponto mais alto do ombro até onde a peça termina. Esta medida é da roupa, não do corpo.",
    },
    {
        titulo: "Manga",
        texto: "Do ombro até o punho, com o braço levemente dobrado.",
    },
];

/** Regras que valem para qualquer medida. */
const CUIDADOS = [
    "Use a fita métrica de costura, nunca a trena de obra: ela precisa acompanhar a curva do corpo.",
    "Meça sobre roupa leve ou sobre a pele. Casaco e jeans grosso somam centímetros que não existem.",
    "A fita encosta, mas não aperta. Puxar a fita é o jeito mais rápido de comprar um tamanho menor do que o seu.",
    "Se puder, peça a alguém para medir por você — sozinha, a fita tende a subir nas costas.",
    "Entre dois tamanhos, escolha o maior quando quiser caimento mais solto e o menor quando quiser marcado.",
];

/** 92 -> "92 cm"; ausente -> travessão (nunca "null" nem célula em branco). */
function formatarCm(valor) {
    if (valor === null || valor === undefined) return null;
    const numero = Number(valor);
    if (!Number.isFinite(numero)) return null;
    // A formatação sai para uma variável de propósito: interpolar a chamada
    // inteira deixaria um identificador em inglês dentro do texto da interface.
    const medida = numero.toLocaleString("pt-BR", { maximumFractionDigits: 1 });
    return `${medida} cm`;
}

function Cabecalho() {
    return (
        <header className="max-w-2xl">
            <p className="eyebrow">Antes de escolher o tamanho</p>
            <h1 className="mt-4 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                Guia de medidas
            </h1>
            <p className="mt-5 text-base leading-relaxed text-ink-soft">
                As tabelas abaixo trazem as medidas em centímetros usadas nas nossas peças.
                Compare com as suas antes de comprar: cinco minutos com a fita métrica evitam
                uma troca inteira.
            </p>
        </header>
    );
}

/** Uma tabela renderizada. Isolada porque a page ja carrega estado demais. */
function TabelaMedidas({ tabela }) {
    if (!tabela || tabela.linhas.length === 0) {
        return (
            <p className="border border-sand bg-linen px-6 py-10 text-sm leading-relaxed text-ink-soft">
                Esta tabela ainda não tem tamanhos cadastrados. Fale com a gente que conferimos a
                peça na régua para você.
            </p>
        );
    }

    return (
        <>
            {/* O scroll vive AQUI dentro. `tabIndex` porque bloco rolável precisa
                ser alcançável pelo teclado. */}
            <div
                className="overflow-x-auto border border-sand focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-2"
                tabIndex={0}
                role="region"
                aria-label={`Medidas da tabela ${tabela.nome}`}
            >
                <table className="w-full min-w-[38rem] border-collapse text-sm">
                    <caption className="sr-only">
                        Medidas por tamanho, em centímetros
                    </caption>
                    <thead>
                        <tr className="border-b border-sand bg-linen text-left">
                            <th scope="col" className="eyebrow px-4 py-3">
                                Tamanho
                            </th>
                            {COLUNAS.map((coluna) => (
                                <th key={coluna.chave} scope="col" className="eyebrow px-4 py-3">
                                    {coluna.rotulo}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {tabela.linhas.map((linha, i) => (
                            <tr
                                key={linha.idTamanho ?? `${tabela.id}-${i}`}
                                className={`border-b border-sand last:border-b-0 ${
                                    i % 2 === 1 ? "bg-linen/60" : ""
                                }`}
                            >
                                <th
                                    scope="row"
                                    className="px-4 py-3 text-left font-sans text-xs uppercase tracking-widest text-ink"
                                >
                                    {linha.codigoTamanho}
                                </th>
                                {COLUNAS.map((coluna) => {
                                    const medida = formatarCm(linha[coluna.chave]);
                                    return (
                                        <td
                                            key={coluna.chave}
                                            className="px-4 py-3 tabular-nums text-ink-soft"
                                        >
                                            {medida ?? (
                                                <span
                                                    className="text-taupe"
                                                    title="Medida não cadastrada para este tamanho"
                                                >
                                                    —
                                                </span>
                                            )}
                                        </td>
                                    );
                                })}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {tabela.observacao && (
                <p className="mt-5 max-w-2xl text-sm leading-relaxed text-ink-soft">
                    {tabela.observacao}
                </p>
            )}
        </>
    );
}

export default function GuiaMedidas() {
    const { tabelas, isLoading, isError, refetch } = useTabelasMedidas();

    // Só o id fica em estado. A tabela é derivada, então uma lista que chega
    // depois (ou muda) nunca deixa a tela apontando para um id que sumiu.
    const [idSelecionado, setIdSelecionado] = useState(null);
    const tabela = tabelas.find((t) => t.id === idSelecionado) ?? tabelas[0] ?? null;

    return (
        <div className="animate-fade-up">
            <div className="shell py-12 lg:py-16">
                <Cabecalho />

                <div className="mt-12">
                    {isLoading ? (
                        <div className="flex flex-col gap-4">
                            <Skeleton className="h-9 w-64" />
                            <Skeleton className="h-64 w-full" />
                        </div>
                    ) : isError ? (
                        <div className="border border-sand bg-linen px-6 py-14 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                Não foi possível carregar as tabelas de medidas.
                            </p>
                            <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                                Pode ter sido a conexão. Tente de novo em instantes.
                            </p>
                            <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                                Tentar de novo
                            </Botao>
                        </div>
                    ) : tabelas.length === 0 ? (
                        <div className="border border-sand bg-linen px-6 py-14 text-center">
                            <p className="font-display text-xl tracking-tight text-ink">
                                Nenhuma tabela de medidas cadastrada ainda.
                            </p>
                            <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-ink-soft">
                                Enquanto isso, cada peça da vitrine mostra a própria ficha com o
                                que já foi medido. Se ficar em dúvida no tamanho, fale com a
                                gente antes de comprar.
                            </p>
                            <Botao to="/catalogo" variante="contorno" className="mt-6">
                                Ver as peças
                            </Botao>
                        </div>
                    ) : (
                        <>
                            {/* Seletor de tabela: só aparece quando há escolha a fazer. */}
                            {tabelas.length > 1 && (
                                <div
                                    role="tablist"
                                    aria-label="Tabelas de medidas"
                                    className="mb-8 flex flex-wrap gap-2 border-b border-sand pb-4"
                                >
                                    {tabelas.map((item) => {
                                        const ativa = tabela?.id === item.id;
                                        return (
                                            <button
                                                key={item.id}
                                                type="button"
                                                role="tab"
                                                aria-selected={ativa}
                                                onClick={() => setIdSelecionado(item.id)}
                                                className={`h-10 border px-5 font-sans text-xs uppercase tracking-widest transition-colors ${
                                                    ativa
                                                        ? "border-ink bg-ink text-bone"
                                                        : "border-sand text-ink-soft hover:border-ink hover:text-ink"
                                                }`}
                                            >
                                                {item.nome}
                                            </button>
                                        );
                                    })}
                                </div>
                            )}

                            <h2 className="font-display text-xl tracking-tight text-ink">
                                {tabela?.nome}
                            </h2>
                            <p className="mb-5 mt-2 text-sm leading-relaxed text-ink-soft">
                                Medidas em centímetros. Onde aparece um travessão, aquela medida
                                não se aplica à peça ou ainda não foi cadastrada.
                            </p>

                            <TabelaMedidas tabela={tabela} />
                        </>
                    )}
                </div>
            </div>

            {/* ------------------------------------------------------ COMO MEDIR */}
            <section className="border-y border-sand bg-linen">
                <div className="shell py-16 lg:py-20">
                    <div className="max-w-2xl">
                        <p className="eyebrow">Com a fita na mão</p>
                        <h2 className="mt-4 font-display text-2xl tracking-tight text-ink">
                            Como tirar as suas medidas
                        </h2>
                        <p className="mt-5 text-base leading-relaxed text-ink-soft">
                            As medidas das tabelas são do CORPO, salvo onde estiver escrito o
                            contrário — comprimento e manga descrevem a peça pronta.
                        </p>
                    </div>

                    <div className="mt-10 grid gap-x-10 gap-y-8 sm:grid-cols-2 lg:grid-cols-3">
                        {COMO_MEDIR.map((item) => (
                            <article key={item.titulo}>
                                <div className="filete" />
                                <h3 className="mt-5 font-display text-lg tracking-tight text-ink">
                                    {item.titulo}
                                </h3>
                                <p className="mt-2 text-sm leading-relaxed text-ink-soft">
                                    {item.texto}
                                </p>
                            </article>
                        ))}
                    </div>

                    <div className="mt-14 max-w-3xl">
                        <h3 className="font-display text-lg tracking-tight text-ink">
                            Cinco cuidados que mudam o resultado
                        </h3>
                        <ul className="mt-5 flex flex-col gap-3">
                            {CUIDADOS.map((cuidado) => (
                                <li
                                    key={cuidado}
                                    className="flex gap-3 text-sm leading-relaxed text-ink-soft"
                                >
                                    <span aria-hidden="true" className="text-brass">
                                        ✦
                                    </span>
                                    <span>{cuidado}</span>
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>
            </section>

            {/* ------------------------------------------------------ FECHAMENTO */}
            <div className="shell py-16 text-center">
                <p className="mx-auto max-w-lg text-base leading-relaxed text-ink-soft">
                    Ficou entre dois tamanhos? A troca do primeiro pedido é combinada com a
                    gente, e as condições estão escritas na política de trocas.
                </p>
                <div className="mt-8 flex flex-wrap justify-center gap-4">
                    <Botao to="/catalogo">Ver a coleção</Botao>
                    <Botao to="/politicas/trocas" variante="contorno">
                        Política de trocas
                    </Botao>
                </div>
            </div>
        </div>
    );
}
