import { useEffect, useRef, useState } from "react";
import { FiArrowDown, FiArrowUp, FiImage, FiStar, FiTrash2, FiUpload } from "react-icons/fi";
import Botao from "@/components/ui/Botao.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import EstadoVazio from "./EstadoVazio.jsx";
import { Swatch } from "./SeletorCor.jsx";
import { FORMATOS_ACEITOS, TAMANHO_MAXIMO_BYTES } from "@/services/admin/midiasAdminService.js";
import { LIMITES } from "@/lib/dominioCatalogo.js";

/**
 * Galeria do produto: envio com pré-visualização, reordenação, capa e vínculo
 * de foto com cor.
 *
 * Três detalhes do backend que a tela precisa respeitar:
 *
 * - Enviar a imagem e colocá-la na galeria são DUAS chamadas: `POST /midias/upload`
 *   grava o arquivo no acervo, `POST /produtos/{id}/midias` cria o vínculo.
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

const TIPOS = FORMATOS_ACEITOS.split(",");

export default function UploadImagens({
    galeria = [],
    cores = [],
    carregando = false,
    enviando = false,
    onEnviar,
    onRemover,
    onReordenar,
    onTrocarCor,
}) {
    const inputRef = useRef(null);
    const [arquivo, setArquivo] = useState(null);
    const [previa, setPrevia] = useState(null);
    const [altText, setAltText] = useState("");
    const [idCor, setIdCor] = useState("");
    const [erro, setErro] = useState(null);

    // A URL de objeto segura o arquivo em memória: revogar é obrigatório.
    useEffect(() => {
        if (!arquivo) {
            setPrevia(null);
            return undefined;
        }
        const url = URL.createObjectURL(arquivo);
        setPrevia(url);
        return () => URL.revokeObjectURL(url);
    }, [arquivo]);

    const escolher = (evento) => {
        const escolhido = evento.target.files?.[0] ?? null;
        setErro(null);

        if (!escolhido) {
            setArquivo(null);
            return;
        }

        if (!TIPOS.includes(escolhido.type)) {
            setErro("Formato não aceito. Envie a imagem em JPEG, PNG, WebP ou AVIF.");
            setArquivo(null);
            return;
        }

        if (escolhido.size > TAMANHO_MAXIMO_BYTES) {
            setErro(
                `A imagem tem ${formatarBytes(escolhido.size)} e o limite é ${formatarBytes(TAMANHO_MAXIMO_BYTES)}.`,
            );
            setArquivo(null);
            return;
        }

        setArquivo(escolhido);
    };

    const limparEscolha = () => {
        setArquivo(null);
        setAltText("");
        setIdCor("");
        setErro(null);
        if (inputRef.current) inputRef.current.value = "";
    };

    const enviar = async () => {
        if (!arquivo) return;
        await onEnviar?.({
            arquivo,
            altText: altText.trim(),
            idCor: idCor === "" ? null : Number(idCor),
        });
        limparEscolha();
    };

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
                            Enviar imagem
                        </h2>
                        <p className="mt-1 text-sm leading-relaxed text-ink-soft">
                            JPEG, PNG, WebP ou AVIF, até {formatarBytes(TAMANHO_MAXIMO_BYTES)}.
                            Foto de moda é retrato: a vitrine recorta em 3:4.
                        </p>
                    </div>
                </div>

                <div className="grid grid-cols-1 gap-6 lg:grid-cols-[12rem,1fr]">
                    <div className="aspect-product w-full max-w-[12rem] border border-sand bg-base-100">
                        {previa ? (
                            <img
                                src={previa}
                                alt="Pré-visualização da imagem escolhida"
                                className="h-full w-full object-cover"
                            />
                        ) : (
                            <div className="flex h-full w-full items-center justify-center text-taupe">
                                <FiImage size={28} aria-hidden="true" />
                            </div>
                        )}
                    </div>

                    <div className="flex flex-col gap-4">
                        <div className="flex flex-col gap-1.5">
                            <label htmlFor="arquivo-galeria" className="eyebrow">
                                Arquivo
                            </label>
                            <input
                                id="arquivo-galeria"
                                ref={inputRef}
                                type="file"
                                accept={FORMATOS_ACEITOS}
                                onChange={escolher}
                                className="w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-sm text-ink file:mr-4 file:border-0 file:bg-linen file:px-3 file:py-1.5 file:font-sans file:text-xs file:uppercase file:tracking-widest file:text-ink"
                            />
                            {erro && (
                                <p role="alert" className="text-xs text-danger">
                                    {erro}
                                </p>
                            )}
                            {arquivo && !erro && (
                                <p className="text-xs text-ink-soft">
                                    {arquivo.name} · {formatarBytes(arquivo.size)}
                                </p>
                            )}
                        </div>

                        <Campo
                            label="Texto alternativo"
                            value={altText}
                            maxLength={LIMITES.altText}
                            placeholder="Vestido midi em linho, cor terracota, de frente"
                            ajuda="Descreve a foto para quem usa leitor de tela e para o buscador."
                            onChange={(e) => setAltText(e.target.value)}
                        />

                        <Campo
                            label="Cor desta foto"
                            como="select"
                            value={idCor}
                            ajuda="Vincular a foto a uma cor troca a galeria quando o cliente clica no swatch. Deixe em branco para uma foto neutra."
                            onChange={(e) => setIdCor(e.target.value)}
                        >
                            <option value="">Foto neutra, vale para todas as cores</option>
                            {cores.map((cor) => (
                                <option key={cor.id} value={cor.id}>
                                    {cor.nome}
                                </option>
                            ))}
                        </Campo>

                        <div className="flex flex-wrap gap-2">
                            <Botao
                                onClick={enviar}
                                disabled={!arquivo || enviando}
                                carregando={enviando}
                            >
                                <FiUpload size={14} aria-hidden="true" />
                                Enviar para a galeria
                            </Botao>
                            {arquivo && (
                                <Botao variante="texto" onClick={limparEscolha} disabled={enviando}>
                                    Descartar
                                </Botao>
                            )}
                        </div>
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
                        mensagem="Envie a primeira imagem acima. A foto que ficar em primeiro lugar na ordem vira a capa da vitrine."
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
                                    <p className="truncate font-sans text-xs text-ink-soft">
                                        {foto.altText || "Sem texto alternativo"}
                                    </p>

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
