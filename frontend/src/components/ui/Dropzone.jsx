import { useCallback, useRef, useState } from "react";
import { FiCheck, FiUploadCloud, FiX } from "react-icons/fi";

/**
 * Área de arrastar-e-soltar reutilizável, com fila de status por lote.
 *
 * Uso: o chamador passa `aceita`/`tamanhoMaximoBytes` (validação client-side, mesma regra do
 * backend) e `onArquivos(validos)` — chamado UMA VEZ por lote (drop ou seleção), com a lista já
 * filtrada dos arquivos válidos. O Dropzone não sabe nada de produto, cor ou galeria: quem
 * decide COMO subir cada arquivo é sempre o chamador (`UploadImagens` faz sequencial por causa
 * da capa; `Midias` pode fazer em paralelo).
 */
const KB = 1024;

function formatarBytes(bytes) {
    const n = Number(bytes) || 0;
    if (n < KB) return `${n} B`;
    if (n < KB * KB) return `${(n / KB).toFixed(0)} KB`;
    return `${(n / (KB * KB)).toFixed(1)} MB`;
}

let proximoIdFila = 0;

export default function Dropzone({
    aceita,
    multiplo = true,
    tamanhoMaximoBytes,
    onArquivos,
    titulo = "Clique para enviar",
    subtitulo = "ou arraste as imagens aqui",
    nota = "Vários arquivos de uma vez",
}) {
    const inputRef = useRef(null);
    const [arrastando, setArrastando] = useState(false);
    const [fila, setFila] = useState([]); // [{id, nome, status: 'enviando'|'ok'|'erro', erro?}]

    const tipos = aceita ? aceita.split(",") : null;

    const validar = useCallback(
        (arquivo) => {
            if (tipos && !tipos.includes(arquivo.type)) {
                return "Formato não aceito (use JPEG, PNG, WebP ou AVIF).";
            }
            if (tamanhoMaximoBytes && arquivo.size > tamanhoMaximoBytes) {
                return `Arquivo de ${formatarBytes(arquivo.size)}, acima do limite de ${formatarBytes(tamanhoMaximoBytes)}.`;
            }
            return null;
        },
        [tipos, tamanhoMaximoBytes],
    );

    const processarArquivos = useCallback(
        async (lista) => {
            const arquivos = Array.from(lista ?? []);
            if (arquivos.length === 0) return;

            const validos = [];
            const entradas = arquivos.map((arquivo) => {
                const erro = validar(arquivo);
                if (!erro) validos.push(arquivo);
                return {
                    id: proximoIdFila++,
                    nome: arquivo.name,
                    status: erro ? "erro" : "enviando",
                    erro,
                };
            });

            setFila((atual) => [...atual, ...entradas]);

            const idsEnviando = entradas.filter((e) => e.status === "enviando").map((e) => e.id);

            if (validos.length > 0) {
                await onArquivos?.(validos);
            }

            // Sem retorno por-arquivo do lote — quando a chamada volta, a lista ja foi
            // revalidada (ou nao). Marca a fila inteira como concluida e some com os
            // itens depois de um instante, pra dar tempo de ler o "enviado".
            setFila((atual) =>
                atual.map((item) =>
                    idsEnviando.includes(item.id) ? { ...item, status: "ok" } : item,
                ),
            );
            setTimeout(() => {
                setFila((atual) => atual.filter((item) => !idsEnviando.includes(item.id)));
            }, 2200);
        },
        [validar, onArquivos],
    );

    const aoSoltar = (evento) => {
        evento.preventDefault();
        setArrastando(false);
        processarArquivos(evento.dataTransfer?.files);
    };

    const aoEscolher = (evento) => {
        processarArquivos(evento.target.files);
        evento.target.value = ""; // permite escolher o mesmo arquivo de novo depois
    };

    const descartarDaFila = (id) => setFila((atual) => atual.filter((item) => item.id !== id));

    return (
        <div>
            <div
                role="button"
                tabIndex={0}
                onClick={() => inputRef.current?.click()}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") inputRef.current?.click();
                }}
                onDragOver={(e) => {
                    e.preventDefault();
                    setArrastando(true);
                }}
                onDragLeave={() => setArrastando(false)}
                onDrop={aoSoltar}
                className={`flex min-h-[10rem] cursor-pointer flex-col items-center justify-center gap-2 border-2 border-dashed px-6 py-8 text-center transition-colors ${
                    arrastando ? "border-olive bg-olive/5" : "border-sand bg-base-100 hover:border-taupe"
                }`}
            >
                <FiUploadCloud
                    size={28}
                    className={arrastando ? "text-olive" : "text-taupe"}
                    aria-hidden="true"
                />
                <p className="font-sans text-sm text-ink">
                    <span className="font-medium text-olive">{titulo}</span> {subtitulo}
                </p>
                {nota && <p className="text-xs text-taupe">{nota}</p>}
                <input
                    ref={inputRef}
                    type="file"
                    accept={aceita}
                    multiple={multiplo}
                    onChange={aoEscolher}
                    className="sr-only"
                />
            </div>

            {fila.length > 0 && (
                <ul className="mt-3 flex flex-col gap-1.5">
                    {fila.map((item) => (
                        <li
                            key={item.id}
                            className="flex items-center gap-2.5 border border-sand bg-base-100 px-3 py-2 text-xs"
                        >
                            {item.status === "enviando" && (
                                <span
                                    className="loading loading-spinner loading-xs shrink-0 text-olive"
                                    aria-hidden="true"
                                />
                            )}
                            {item.status === "ok" && (
                                <FiCheck size={14} className="shrink-0 text-success" aria-hidden="true" />
                            )}
                            {item.status === "erro" && (
                                <FiX size={14} className="shrink-0 text-danger" aria-hidden="true" />
                            )}
                            <span className="truncate text-ink-soft">{item.nome}</span>
                            {item.erro && <span className="truncate text-danger">· {item.erro}</span>}
                            {item.status === "erro" && (
                                <button
                                    type="button"
                                    aria-label="Descartar da fila"
                                    onClick={() => descartarDaFila(item.id)}
                                    className="ml-auto shrink-0 text-taupe hover:text-ink"
                                >
                                    <FiX size={14} />
                                </button>
                            )}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
