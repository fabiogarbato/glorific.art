import { useEffect, useMemo, useState } from "react";
import { FiCheck, FiMessageSquare, FiStar, FiX } from "react-icons/fi";

import BadgeStatus from "@/components/admin/BadgeStatus.jsx";
import {
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
} from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

import { useAcoesAvaliacao, useAvaliacoesAdmin } from "@/hooks/useAvaliacoesAdmin.js";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";
import { AVALIACAO_PENDENTE, CAIMENTO, descrever, STATUS_AVALIACAO } from "@/lib/statusAdmin.js";
import { formatarDataHora, formatarRelativo } from "@/utils/datas.js";

/**
 * Fila de moderação (policy GestaoCatalogo).
 *
 * Toda avaliação nasce pendente e nenhuma chega à vitrine sem passar por aqui.
 * A decisão é de risco de marca, não de produto: comentário aberto em loja
 * cristã custa caro para despublicar depois de circular.
 *
 * O moderador vê nome e e-mail de quem escreveu — é ele quem julga se o texto é
 * legítimo. Esses dois campos nunca aparecem na vitrine, por isso o backend tem
 * um DTO separado para esta tela.
 */

function Estrelas({ nota }) {
    return (
        <span
            className="inline-flex items-center gap-0.5"
            aria-label={`Nota ${nota} de 5`}
            title={`Nota ${nota} de 5`}
        >
            {[1, 2, 3, 4, 5].map((i) => (
                <FiStar
                    key={i}
                    size={13}
                    aria-hidden="true"
                    className={i <= nota ? "fill-brass text-brass" : "text-sand"}
                />
            ))}
        </span>
    );
}

export default function Avaliacoes() {
    const [status, setStatus] = useState(String(AVALIACAO_PENDENTE));
    const [pagina, setPagina] = useState(1);
    const [rejeicao, setRejeicao] = useState(null);

    useEffect(() => {
        setPagina(1);
    }, [status]);

    const filtros = useMemo(
        () => ({
            status: status === "" ? undefined : Number(status),
            page: pagina,
            pageSize: ITENS_POR_PAGINA,
        }),
        [status, pagina],
    );

    const {
        avaliacoes,
        total,
        totalPaginas,
        tamanhoPagina,
        isLoading,
        isError,
        refetch,
    } = useAvaliacoesAdmin(filtros);

    const { aprovar, rejeitar } = useAcoesAvaliacao();

    const confirmarRejeicao = () => {
        const texto = rejeicao.motivo.trim();
        if (texto.length < 3) {
            setRejeicao({ ...rejeicao, erro: "O motivo precisa ter ao menos 3 caracteres." });
            return;
        }
        rejeitar.mutate(
            { id: rejeicao.avaliacao.id, motivo: texto },
            { onSuccess: () => setRejeicao(null) },
        );
    };

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Operação"
                titulo="Moderação de avaliações"
                descricao="Aprovar publica na página do produto e recalcula a nota média. Rejeitar exige motivo — sem ele não há como responder a quem perguntar por que a avaliação sumiu."
            />

            <div className="mb-6 flex flex-wrap items-end gap-4 border border-sand bg-linen p-4">
                <Campo
                    label="Situação"
                    como="select"
                    value={status}
                    onChange={(e) => setStatus(e.target.value)}
                    containerClassName="w-56"
                >
                    {STATUS_AVALIACAO.map((s) => (
                        <option key={s.valor} value={s.valor}>
                            {s.rotulo}
                        </option>
                    ))}
                    <option value="">Todas</option>
                </Campo>

                <p className="pb-2 text-xs text-ink-soft">
                    {total} avaliação(ões) nesta situação.
                </p>
            </div>

            {isError ? (
                <EstadoErro mensagem="A fila de moderação não pôde ser carregada." onTentarDeNovo={refetch} />
            ) : isLoading ? (
                <div className="flex flex-col gap-4">
                    {Array.from({ length: 3 }).map((_, i) => (
                        <Skeleton key={i} className="h-44 w-full" />
                    ))}
                </div>
            ) : avaliacoes.length === 0 ? (
                <EstadoVazio
                    Icone={FiMessageSquare}
                    titulo={
                        Number(status) === AVALIACAO_PENDENTE
                            ? "Fila vazia"
                            : "Nenhuma avaliação nesta situação"
                    }
                    mensagem={
                        Number(status) === AVALIACAO_PENDENTE
                            ? "Nada esperando moderação. Quando alguém avaliar uma peça, o texto aparece aqui antes de ir para a vitrine."
                            : "Troque a situação no filtro acima para ver as outras avaliações."
                    }
                />
            ) : (
                <>
                    <ul className="flex flex-col gap-4">
                        {avaliacoes.map((a) => {
                            const caimento = a.caimento
                                ? descrever(CAIMENTO, a.caimento).rotulo
                                : null;

                            return (
                                <li key={a.id} className="border border-sand bg-base-100 p-5">
                                    <div className="flex flex-wrap items-start justify-between gap-3">
                                        <div className="min-w-0">
                                            <p className="text-sm text-ink">{a.nomeProduto}</p>
                                            <p className="text-xs text-ink-soft">
                                                {a.nomeUsuario || "Sem nome"} · {a.emailUsuario}
                                            </p>
                                        </div>
                                        <div className="flex flex-wrap items-center gap-2">
                                            {a.compraVerificada && (
                                                <Badge variante="sucesso">Compra verificada</Badge>
                                            )}
                                            <BadgeStatus mapa={STATUS_AVALIACAO} valor={a.status} />
                                        </div>
                                    </div>

                                    <div className="mt-3 flex flex-wrap items-center gap-3">
                                        <Estrelas nota={a.nota} />
                                        <span className="text-xs text-ink-soft">
                                            {formatarDataHora(a.dataCriacao)} ·{" "}
                                            {formatarRelativo(a.dataCriacao)}
                                        </span>
                                    </div>

                                    {a.titulo && (
                                        <p className="mt-3 font-display text-lg tracking-tight text-ink">
                                            {a.titulo}
                                        </p>
                                    )}

                                    {a.comentario && (
                                        <p className="mt-2 whitespace-pre-line text-sm leading-relaxed text-ink-soft">
                                            {a.comentario}
                                        </p>
                                    )}

                                    {(a.tamanhoComprado ||
                                        caimento ||
                                        a.alturaClienteCm ||
                                        a.pesoClienteKg ||
                                        a.recomenda != null) && (
                                        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-soft">
                                            {a.tamanhoComprado && (
                                                <li>Comprou o tamanho {a.tamanhoComprado}</li>
                                            )}
                                            {caimento && <li>Caimento: {caimento}</li>}
                                            {a.alturaClienteCm && <li>{a.alturaClienteCm} cm</li>}
                                            {a.pesoClienteKg && <li>{a.pesoClienteKg} kg</li>}
                                            {a.recomenda != null && (
                                                <li>
                                                    {a.recomenda
                                                        ? "Recomenda a peça"
                                                        : "Não recomenda a peça"}
                                                </li>
                                            )}
                                        </ul>
                                    )}

                                    {a.midias?.length > 0 && (
                                        <ul className="mt-4 flex flex-wrap gap-2">
                                            {a.midias.map((m) => (
                                                <li key={m.id}>
                                                    <img
                                                        src={m.url}
                                                        alt={m.altText || "Foto enviada na avaliação"}
                                                        loading="lazy"
                                                        className="h-24 w-20 object-cover"
                                                    />
                                                </li>
                                            ))}
                                        </ul>
                                    )}

                                    {a.motivoRejeicao && (
                                        <p className="mt-4 border-l-2 border-danger bg-linen px-3 py-2 text-xs text-ink-soft">
                                            Rejeitada em {formatarDataHora(a.moderadaEm)}:{" "}
                                            {a.motivoRejeicao}
                                        </p>
                                    )}

                                    <div className="mt-5 flex flex-wrap gap-2 border-t border-sand pt-4">
                                        <Botao
                                            tamanho="sm"
                                            disabled={a.status === 2}
                                            carregando={
                                                aprovar.isPending && aprovar.variables === a.id
                                            }
                                            onClick={() => aprovar.mutate(a.id)}
                                        >
                                            <FiCheck size={14} aria-hidden="true" /> Aprovar
                                        </Botao>
                                        <Botao
                                            variante="perigo"
                                            tamanho="sm"
                                            disabled={a.status === 3}
                                            onClick={() =>
                                                setRejeicao({ avaliacao: a, motivo: "", erro: "" })
                                            }
                                        >
                                            <FiX size={14} aria-hidden="true" /> Rejeitar
                                        </Botao>
                                    </div>
                                </li>
                            );
                        })}
                    </ul>

                    <Paginacao
                        className="mt-6"
                        paginaAtual={pagina}
                        totalPaginas={totalPaginas}
                        totalItens={total}
                        itensPorPagina={tamanhoPagina}
                        onMudarPagina={setPagina}
                    />
                </>
            )}

            <Modal
                isOpen={!!rejeicao}
                onClose={() => setRejeicao(null)}
                titulo="Rejeitar avaliação"
                largura="sm"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setRejeicao(null)}>
                            Voltar
                        </Botao>
                        <Botao
                            variante="perigo"
                            onClick={confirmarRejeicao}
                            carregando={rejeitar.isPending}
                        >
                            Rejeitar
                        </Botao>
                    </>
                }
            >
                {rejeicao && (
                    <>
                        <p className="mb-4 text-sm leading-relaxed">
                            O motivo fica registrado junto com quem moderou e quando. Se a avaliação
                            já estava publicada, a nota média do produto é recalculada.
                        </p>
                        <Campo
                            label="Motivo"
                            como="textarea"
                            obrigatorio
                            maxLength={400}
                            value={rejeicao.motivo}
                            erro={rejeicao.erro}
                            onChange={(e) =>
                                setRejeicao({ ...rejeicao, motivo: e.target.value, erro: "" })
                            }
                        />
                    </>
                )}
            </Modal>
        </div>
    );
}
