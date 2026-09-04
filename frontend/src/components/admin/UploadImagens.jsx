import { useState } from "react";
import { FiArrowDown, FiArrowUp, FiCheck, FiImage, FiStar, FiTrash2, FiZap } from "react-icons/fi";
import Badge from "@/components/ui/Badge.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import Dropzone from "@/components/ui/Dropzone.jsx";
import EstadoVazio from "./EstadoVazio.jsx";
import { Swatch } from "./SeletorCor.jsx";
import {
    FORMATOS_ACEITOS,
    TAMANHO_MAXIMO_BYTES,
    midiasAdminService,
} from "@/services/admin/midiasAdminService.js";

/**
 * Galeria do produto: arrastar-e-soltar com envio automático, reordenação,
 * capa e vínculo de foto com cor.
 *
 * O upload é imediato — arrastou (ou escolheu no clique), já sobe. Não existe
 * mais um botão "Enviar" separado: era exatamente esse segundo passo que
 * ficava esquecido (a peça anterior guardava o arquivo escolhido e só
 * disparava a chamada de rede num clique à parte).
 *
 * Três detalhes do backend que a tela precisa respeitar:
 *
 * - Enviar a imagem e colocá-la na galeria são DUAS chamadas: `POST /midias/upload`
 *   grava o arquivo no acervo, `POST /produtos/{id}/midias` cria o vínculo.
 *   O componente-pai (`FormProduto`) processa o lote em sequência para as
 *   duas chamadas não brigarem pela mesma posição/capa.
 * - A capa não tem endpoint próprio. `PUT /midias/ordem` marca a PRIMEIRA
 *   posição como capa — por isso "definir capa" move a foto para o começo.
 * - A ordem é enviada com o id da LINHA da galeria; a remoção, com o id da MÍDIA.
 */
const KB = 1024;

function formatarBytes(bytes) {
    const n = Number(bytes) || 0;
    if (n < KB) return `${n} B`;
    if (n < KB * KB) return `${(n / KB).toFixed(0)} KB`;
    return `${(n / (KB * KB)).toFixed(1)} MB`;
}

/** Campo de texto alternativo editado inline, direto no card da galeria — com um botao de IA
 * que le a propria foto e os alt texts de outras imagens do acervo como referencia. */
function AltTextInline({ foto, onSalvar }) {
    const [valor, setValor] = useState(foto.altText ?? "");
    const [editando, setEditando] = useState(false);
    const [gerando, setGerando] = useState(false);
    const [salvando, setSalvando] = useState(false);
    const [salvo, setSalvo] = useState(false);

    const salvar = async () => {
        setEditando(false);
        const limpo = valor.trim();
        if (limpo === (foto.altText ?? "")) return;

        setSalvando(true);
        try {
            await onSalvar?.({ idMidia: foto.idMidia, altText: limpo });
            setSalvo(true);
            setTimeout(() => setSalvo(false), 1800);
        } finally {
            setSalvando(false);
        }
    };

    const gerarComIa = async () => {
        setGerando(true);
        try {
            const sugestao = await midiasAdminService.gerarTextoAlternativo(foto.idMidia);
            setValor(sugestao);
            setEditando(true);
        } catch {
            /* toast de erro ja emitido pelo interceptor */
        } finally {
            setGerando(false);
        }
    };

    const botaoIa = (
        <button
            type="button"
            onMouseDown={(e) => e.preventDefault()} // nao dispara o onBlur do input antes do clique
            onClick={gerarComIa}
            disabled={gerando}
            aria-label="Gerar texto alternativo com IA"
            className="shrink-0 text-taupe transition-colors hover:text-olive disabled:opacity-40"
        >
            {gerando ? (
                <span className="loading loading-spinner loading-xs" aria-hidden="true" />
            ) : (
                <FiZap size={12} aria-hidden="true" />
            )}
        </button>
    );

    // Disquete-fantasma: aparece um instante depois de salvar, pra confirmar sem precisar
    // recarregar a tela pra saber se pegou.
    const indicadorSalvo = salvando ? (
        <span
            className="loading loading-spinner loading-xs shrink-0 text-taupe"
            aria-hidden="true"
        />
    ) : salvo ? (
        <FiCheck
            size={14}
            className="shrink-0 text-olive"
            aria-label="Salvo"
        />
    ) : null;

    if (!editando) {
        return (
            <div className="flex items-center gap-2">
                <button
                    type="button"
                    onClick={() => setEditando(true)}
                    className="min-w-0 flex-1 truncate text-left font-sans text-xs text-ink-soft underline decoration-sand decoration-dotted underline-offset-4 transition-colors hover:text-ink hover:decoration-ink"
                >
                    {foto.altText || "Adicionar texto alternativo"}
                </button>
                {indicadorSalvo}
                {botaoIa}
            </div>
        );
    }

    return (
        <div className="flex items-center gap-2">
            <input
                autoFocus
                value={valor}
                onChange={(e) => setValor(e.target.value)}
                onBlur={salvar}
                onKeyDown={(e) => {
                    if (e.key === "Enter") e.currentTarget.blur();
                    if (e.key === "Escape") {
                        setValor(foto.altText ?? "");
                        setEditando(false);
                    }
                }}
                placeholder="Descreva a foto para leitor de tela e busca"
                className="min-w-0 flex-1 border border-olive bg-base-100 px-2 py-1 font-sans text-xs text-ink focus:outline-none"
            />
            {indicadorSalvo}
            {botaoIa}
        </div>
    );
}

export default function UploadImagens({
    galeria = [],
    cores = [],
    carregando = false,
    onEnviarLote,
    onAtualizarAltText,
    onRemover,
    onReordenar,
    onTrocarCor,
}) {
    const [idCorLote, setIdCorLote] = useState("");

    const mover = (indice, destino) => {
        if (destino < 0 || destino >= galeria.length) return;
        const ordenada = [...galeria];
        const [item] = ordenada.splice(indice, 1);
        ordenada.splice(destino, 0, item);
        onReordenar?.(ordenada.map((foto) => foto.id));
    };

    const definirCapa = (indice) => mover(indice, 0);

    return (
        <div className="flex flex-col gap-8">
            {/* --------------------------------------------------- Envio */}
            <section className="border border-sand bg-linen/50 p-4 sm:p-6">
                <div className="mb-5 flex items-start gap-3">
                    <FiImage size={18} className="mt-1 shrink-0 text-ink-soft" aria-hidden="true" />
                    <div>
                        <h2 className="font-display text-xl tracking-tight text-ink">
                            Enviar imagens
                        </h2>
                        <p className="mt-1 text-sm leading-relaxed text-ink-soft">
                            Arraste quantas quiser, ou clique para escolher. JPEG, PNG, WebP ou
                            AVIF, até {formatarBytes(TAMANHO_MAXIMO_BYTES)} cada — o envio começa
                            na hora. Foto de moda é retrato: a vitrine recorta em 3:4.
                        </p>
                    </div>
                </div>

                <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
                    <div className="lg:w-64 lg:shrink-0">
                        <label htmlFor="cor-lote" className="eyebrow">
                            Cor destas fotos
                        </label>
                        <select
                            id="cor-lote"
                            value={idCorLote}
                            onChange={(e) => setIdCorLote(e.target.value)}
                            className="mt-1.5 w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-sm text-ink transition-colors focus:border-olive focus:outline-none"
                        >
                            <option value="">Foto neutra, vale para todas as cores</option>
                            {cores.map((cor) => (
                                <option key={cor.id} value={cor.id}>
                                    {cor.nome}
                                </option>
                            ))}
                        </select>
                        <p className="mt-1.5 text-xs text-ink-soft">
                            Vale para o próximo lote enviado. Vincular a foto a uma cor troca a
                            galeria quando o cliente clica no swatch.
                        </p>
                    </div>

                    <div className="flex-1">
                        <Dropzone
                            aceita={FORMATOS_ACEITOS}
                            tamanhoMaximoBytes={TAMANHO_MAXIMO_BYTES}
                            onArquivos={(validos) =>
                                onEnviarLote?.(validos, {
                                    idCor: idCorLote === "" ? null : Number(idCorLote),
                                })
                            }
                        />
                    </div>
                </div>
            </section>

            {/* -------------------------------------------------- Galeria */}
            <section>
                <h2 className="mb-4 font-display text-xl tracking-tight text-ink">
                    Galeria da peça
                    {galeria.length > 0 && (
                        <span className="ml-2 font-sans text-sm text-ink-soft">
                            {galeria.length}
                        </span>
                    )}
                </h2>

                {carregando && (
                    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
                        {Array.from({ length: 4 }).map((_, i) => (
                            <Skeleton key={`sk-${i}`} className="aspect-product w-full" />
                        ))}
                    </div>
                )}

                {!carregando && galeria.length === 0 && (
                    <EstadoVazio
                        titulo="A peça ainda não tem foto"
                        mensagem="Arraste a primeira imagem na área acima. A foto que ficar em primeiro lugar na ordem vira a capa da vitrine."
                    />
                )}

                {!carregando && galeria.length > 0 && (
                    <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                        {galeria.map((foto, indice) => (
                            <li key={foto.id} className="border border-sand bg-base-100">
                                <div className="relative">
                                    <div className="aspect-product w-full bg-linen">
                                        <img
                                            src={foto.url}
                                            alt={foto.altText || "Foto da peça"}
                                            loading="lazy"
                                            className="h-full w-full object-cover"
                                        />
                                    </div>
                                    {foto.ehCapa && (
                                        <Badge variante="destaque" className="absolute left-2 top-2">
                                            Capa
                                        </Badge>
                                    )}
                                </div>

                                <div className="flex flex-col gap-3 p-3">
                                    <AltTextInline foto={foto} onSalvar={onAtualizarAltText} />

                                    <div className="flex flex-col gap-1.5">
                                        <label
                                            htmlFor={`cor-foto-${foto.id}`}
                                            className="eyebrow flex items-center gap-2"
                                        >
                                            {foto.idCor != null && (
                                                <Swatch
                                                    cor={cores.find((c) => c.id === foto.idCor)}
                                                    tamanho={14}
                                                />
                                            )}
                                            Cor da foto
                                        </label>
                                        <select
                                            id={`cor-foto-${foto.id}`}
                                            value={foto.idCor ?? ""}
                                            onChange={(e) =>
                                                onTrocarCor?.({
                                                    item: foto,
                                                    idCor:
                                                        e.target.value === ""
                                                            ? null
                                                            : Number(e.target.value),
                                                })
                                            }
                                            className="w-full border border-sand bg-base-100 px-2 py-2 font-sans text-sm text-ink transition-colors focus:border-olive focus:outline-none"
                                        >
                                            <option value="">Foto neutra</option>
                                            {cores.map((cor) => (
                                                <option key={cor.id} value={cor.id}>
                                                    {cor.nome}
                                                </option>
                                            ))}
                                        </select>
                                    </div>

                                    <div className="flex flex-wrap items-center gap-1 border-t border-sand pt-3">
                                        <button
                                            type="button"
                                            aria-label={`Mover ${foto.altText || "a foto"} para cima`}
                                            disabled={indice === 0}
                                            onClick={() => mover(indice, indice - 1)}
                                            className="flex h-9 w-9 items-center justify-center border border-sand text-ink-soft transition-colors hover:text-ink disabled:opacity-30"
                                        >
                                            <FiArrowUp size={14} />
                                        </button>
                                        <button
                                            type="button"
                                            aria-label={`Mover ${foto.altText || "a foto"} para baixo`}
                                            disabled={indice === galeria.length - 1}
                                            onClick={() => mover(indice, indice + 1)}
                                            className="flex h-9 w-9 items-center justify-center border border-sand text-ink-soft transition-colors hover:text-ink disabled:opacity-30"
                                        >
                                            <FiArrowDown size={14} />
                                        </button>
                                        <button
                                            type="button"
                                            aria-label={`Definir ${foto.altText || "a foto"} como capa`}
                                            disabled={foto.ehCapa}
                                            onClick={() => definirCapa(indice)}
                                            className="flex h-9 items-center gap-1.5 border border-sand px-2.5 font-sans text-xs uppercase tracking-widest text-ink-soft transition-colors hover:text-ink disabled:opacity-30"
                                        >
                                            <FiStar size={13} aria-hidden="true" />
                                            Capa
                                        </button>

                                        <button
                                            type="button"
                                            aria-label={`Remover ${foto.altText || "a foto"} da galeria`}
                                            onClick={() => onRemover?.(foto)}
                                            className="ml-auto flex h-9 w-9 items-center justify-center border border-sand text-ink-soft transition-colors hover:border-danger hover:text-danger"
                                        >
                                            <FiTrash2 size={14} />
                                        </button>
                                    </div>
                                </div>
                            </li>
                        ))}
                    </ul>
                )}

                {!carregando && galeria.length > 0 && (
                    <p className="mt-3 text-xs text-taupe">
                        A primeira foto da ordem é a capa da vitrine. Remover a capa promove a
                        seguinte automaticamente.
                    </p>
                )}
            </section>
        </div>
    );
}
