import { useState } from "react";
import Badge from "@/components/ui/Badge.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Skeleton, { SkeletonTexto } from "@/components/ui/Skeleton.jsx";
import EstrelasNota from "./EstrelasNota.jsx";
import { useAvaliacoes, useResumoAvaliacoes, AVALIACOES_POR_PAGINA } from "@/hooks/useAvaliacoes.js";
import { formatarData } from "@/utils/datas.js";
import { formatarNota, recomendacaoDeTamanho, rotuloCaimento } from "@/lib/vitrine.js";

/**
 * Bloco de avaliacoes da pagina de produto: resumo agregado + lista paginada.
 *
 * O resumo vem do backend ja agregado no banco (nao e soma das 5 da pagina), e
 * o caimento predominante e o campo que faz este bloco valer alguma coisa em
 * moda: "a maioria diz que veste pequeno" e o que evita a devolucao.
 */
const NOTAS = [5, 4, 3, 2, 1];

function BarraDistribuicao({ nota, quantidade, total }) {
    const percentual = total > 0 ? Math.round((quantidade / total) * 100) : 0;

    return (
        <div className="flex items-center gap-3">
            <span className="w-8 shrink-0 text-xs tabular-nums text-ink-soft">{nota} ★</span>
            <span className="h-1.5 flex-1 bg-sand">
                <span
                    className="block h-full bg-brass"
                    style={{ width: `${percentual}%` }}
                    aria-hidden="true"
                />
            </span>
            <span className="w-10 shrink-0 text-right text-xs tabular-nums text-taupe">
                {percentual}%
            </span>
        </div>
    );
}

function Avaliacao({ item }) {
    const caimento = rotuloCaimento(item.caimento);

    const corpo = [
        item.tamanhoComprado ? `Comprou o tamanho ${item.tamanhoComprado}` : null,
        item.alturaClienteCm ? `${item.alturaClienteCm} cm` : null,
        item.pesoClienteKg ? `${Number(item.pesoClienteKg).toLocaleString("pt-BR")} kg` : null,
    ].filter(Boolean);

    return (
        <article className="border-t border-sand py-6">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
                <EstrelasNota nota={item.nota} tamanho={13} />
                <span className="text-sm text-ink">{item.autor}</span>
                {item.compraVerificada && <Badge variante="neutro">Compra verificada</Badge>}
                <span className="ml-auto text-xs text-taupe">
                    {formatarData(item.dataCriacao)}
                </span>
            </div>

            {item.titulo && (
                <h4 className="mt-3 font-sans text-sm font-medium text-ink">{item.titulo}</h4>
            )}

            {item.comentario && (
                <p className="mt-2 whitespace-pre-line text-sm leading-relaxed text-ink-soft">
                    {item.comentario}
                </p>
            )}

            {(caimento || corpo.length > 0 || item.recomenda === true) && (
                <div className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-ink-soft">
                    {caimento && (
                        <span className="border border-sand bg-linen px-2 py-1">{caimento}</span>
                    )}
                    {corpo.length > 0 && <span>{corpo.join(" · ")}</span>}
                    {item.recomenda === true && (
                        <span className="text-olive">Recomenda esta peça</span>
                    )}
                </div>
            )}

            {item.midias?.length > 0 && (
                <ul className="mt-4 flex flex-wrap gap-2">
                    {item.midias.map((midia) => (
                        <li key={midia.id}>
                            <img
                                src={midia.url}
                                alt={midia.altText || `Foto enviada por ${item.autor}`}
                                loading="lazy"
                                className="h-20 w-16 object-cover"
                            />
                        </li>
                    ))}
                </ul>
            )}
        </article>
    );
}

export default function ListaAvaliacoes({ idProduto }) {
    const [pagina, setPagina] = useState(1);

    const { resumo, isLoading: carregandoResumo } = useResumoAvaliacoes(idProduto);
    const {
        avaliacoes,
        total,
        totalPaginas,
        isLoading: carregandoLista,
        isError,
    } = useAvaliacoes(idProduto, pagina);

    if (carregandoResumo || carregandoLista) {
        return (
            <div className="flex flex-col gap-6">
                <Skeleton className="h-8 w-40" />
                <SkeletonTexto linhas={4} />
            </div>
        );
    }

    if (isError) {
        return (
            <p className="text-sm text-ink-soft">
                Não foi possível carregar as avaliações agora. Atualize a página para tentar de
                novo.
            </p>
        );
    }

    const totalAvaliacoes = resumo?.totalAvaliacoes ?? 0;

    if (totalAvaliacoes === 0) {
        return (
            <p className="text-sm leading-relaxed text-ink-soft">
                Esta peça ainda não recebeu avaliações. Se você já comprou, seu relato sobre o
                caimento ajuda muito quem está em dúvida entre dois tamanhos.
            </p>
        );
    }

    const recomendacao = recomendacaoDeTamanho(resumo.caimentoPredominante);

    return (
        <div>
            {/* ----------------------------------------------------- RESUMO */}
            <div className="grid gap-8 sm:grid-cols-2">
                <div>
                    <p className="font-display text-3xl leading-none text-ink">
                        {formatarNota(resumo.notaMedia) ?? "—"}
                    </p>
                    <EstrelasNota
                        nota={resumo.notaMedia}
                        total={totalAvaliacoes}
                        tamanho={16}
                        className="mt-3"
                    />
                    <p className="mt-3 text-sm text-ink-soft">
                        {totalAvaliacoes === 1
                            ? "1 avaliação publicada"
                            : `${totalAvaliacoes} avaliações publicadas`}
                    </p>

                    {resumo.percentualRecomenda !== null && (
                        <p className="mt-2 text-sm text-olive">
                            {resumo.percentualRecomenda}% recomendam esta peça
                        </p>
                    )}
                </div>

                <div className="flex flex-col gap-2">
                    {NOTAS.map((nota) => (
                        <BarraDistribuicao
                            key={nota}
                            nota={nota}
                            quantidade={resumo.distribuicaoPorNota[nota] ?? 0}
                            total={totalAvaliacoes}
                        />
                    ))}
                </div>
            </div>

            {recomendacao && (
                <p className="mt-8 border-l-2 border-brass bg-linen px-4 py-3 text-sm text-ink">
                    {recomendacao}{" "}
                    <span className="text-ink-soft">
                        ({resumo.totalRespostasCaimento}{" "}
                        {resumo.totalRespostasCaimento === 1 ? "resposta" : "respostas"})
                    </span>
                </p>
            )}

            {/* ------------------------------------------------------ LISTA */}
            <div className="mt-8">
                {avaliacoes.map((item) => (
                    <Avaliacao key={item.id} item={item} />
                ))}
            </div>

            {totalPaginas > 1 && (
                <Paginacao
                    className="mt-8 border-t border-sand pt-6"
                    paginaAtual={pagina}
                    totalPaginas={totalPaginas}
                    onMudarPagina={setPagina}
                    totalItens={total}
                    itensPorPagina={AVALIACOES_POR_PAGINA}
                />
            )}
        </div>
    );
}
