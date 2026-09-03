import { useEffect, useMemo, useRef, useState } from "react";
import { FiEdit2, FiHardDrive, FiImage, FiTrash2, FiUpload } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import Aviso from "@/components/admin/Aviso.jsx";
import CabecalhoPagina from "@/components/admin/CabecalhoPagina.jsx";
import EstadoVazio from "@/components/admin/EstadoVazio.jsx";
import FaixaKpis from "@/components/admin/FaixaKpis.jsx";
import FiltroBusca from "@/components/admin/FiltroBusca.jsx";
import { COL } from "@/components/admin/chaves.js";

import { useListaAdmin } from "@/hooks/admin/useListaAdmin.js";
import { useOrdenacaoLocal } from "@/hooks/admin/useOrdenacaoLocal.js";
import { useMidiasAdmin, useMutacoesMidia } from "@/hooks/admin/useMidiasAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { LIMITES } from "@/lib/dominioCatalogo.js";
import {
    FORMATOS_ACEITOS,
    TAMANHO_MAXIMO_BYTES,
} from "@/services/admin/midiasAdminService.js";
import { formatarDataHora } from "@/utils/datas.js";

/**
 * Acervo de imagens do catálogo.
 *
 * O acervo é compartilhado: a mesma foto pode estar em várias peças, e por isso
 * remover uma imagem daqui é diferente de tirá-la da galeria de uma peça. Só o
 * texto alternativo é editável — trocar a URL mudaria a foto de todo produto que
 * a referencia sem deixar rastro; quem quer outra imagem sobe outra imagem.
 */
const KB = 1024;

function formatarBytes(bytes) {
    const n = Number(bytes) || 0;
    if (n === 0) return "—";
    if (n < KB) return `${n} B`;
    if (n < KB * KB) return `${(n / KB).toFixed(0)} KB`;
    return `${(n / (KB * KB)).toFixed(1)} MB`;
}

const TIPOS = FORMATOS_ACEITOS.split(",");

export default function Midias() {
    const toast = useToast();
    const inputRef = useRef(null);

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useMidiasAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { enviar, atualizarAltText, remover } = useMutacoesMidia();

    const [arquivo, setArquivo] = useState(null);
    const [previa, setPrevia] = useState(null);
    const [altTextNovo, setAltTextNovo] = useState("");
    const [erroArquivo, setErroArquivo] = useState(null);

    const [edicao, setEdicao] = useState(null);
    const [altTextEdicao, setAltTextEdicao] = useState("");
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!arquivo) {
            setPrevia(null);
            return undefined;
        }
        const url = URL.createObjectURL(arquivo);
        setPrevia(url);
        return () => URL.revokeObjectURL(url);
    }, [arquivo]);

    useEffect(() => {
        if (edicao) setAltTextEdicao(edicao.altText ?? "");
    }, [edicao]);

    const filtradas = useMemo(() => {
        const termo = lista.filtros.q.trim().toLowerCase();
        if (!termo) return itens;
        return itens.filter(
            (m) =>
                (m.altText ?? "").toLowerCase().includes(termo) ||
                (m.url ?? "").toLowerCase().includes(termo),
        );
    }, [itens, lista.filtros.q]);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(filtradas);

    const kpis = useMemo(() => {
        const bytes = itens.reduce((soma, m) => soma + (Number(m.tamanhoBytes) || 0), 0);
        const semAlt = itens.filter((m) => !m.altText).length;

        return [
            { rotulo: "Imagens no acervo", valor: total, Icone: FiImage },
            {
                rotulo: "Peso desta página",
                valor: formatarBytes(bytes),
                Icone: FiHardDrive,
            },
            {
                rotulo: "Sem texto alternativo",
                valor: semAlt,
                Icone: FiEdit2,
                alerta: semAlt > 0,
                ajuda: "Imagem sem descrição não é lida por quem usa leitor de tela.",
            },
        ];
    }, [itens, total]);

    const escolher = (evento) => {
        const escolhido = evento.target.files?.[0] ?? null;
        setErroArquivo(null);

        if (!escolhido) {
            setArquivo(null);
            return;
        }
        if (!TIPOS.includes(escolhido.type)) {
            setErroArquivo("Formato não aceito. Envie a imagem em JPEG, PNG, WebP ou AVIF.");
            setArquivo(null);
            return;
        }
        if (escolhido.size > TAMANHO_MAXIMO_BYTES) {
            setErroArquivo(
                `A imagem tem ${formatarBytes(escolhido.size)} e o limite é ${formatarBytes(TAMANHO_MAXIMO_BYTES)}.`,
            );
            setArquivo(null);
            return;
        }
        setArquivo(escolhido);
    };

    const limparEnvio = () => {
        setArquivo(null);
        setAltTextNovo("");
        setErroArquivo(null);
        if (inputRef.current) inputRef.current.value = "";
    };

    const enviarArquivo = async () => {
        if (!arquivo) return;
        try {
            await enviar.mutateAsync({ arquivo, altText: altTextNovo.trim() });
            toast.success("Imagem adicionada ao acervo.");
            limparEnvio();
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const salvarAltText = async (evento) => {
        evento.preventDefault();
        try {
            await atualizarAltText.mutateAsync({
                id: edicao.id,
                altText: altTextEdicao.trim() || null,
            });
            toast.success("Texto alternativo salvo.");
            setEdicao(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const excluir = async () => {
        try {
            await remover.mutateAsync(confirmar.id);
            toast.success("Imagem removida do acervo.");
            setConfirmar(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const colunas = [
        {
            chave: COL.url,
            titulo: "Imagem",
            render: (m) => (
                <div className="flex min-w-0 items-center gap-3">
                    <img
                        src={m.url}
                        alt={m.altText || "Imagem do acervo"}
                        loading="lazy"
                        className="h-14 w-11 shrink-0 border border-sand object-cover"
                    />
                    <div className="min-w-0">
                        <p className="truncate font-sans text-sm text-ink">
                            {m.altText || "Sem texto alternativo"}
                        </p>
                        <span className="block max-w-[20rem] truncate text-xs text-taupe">
                            {m.url}
                        </span>
                    </div>
                </div>
            ),
        },
        {
            chave: COL.contentType,
            titulo: "Formato",
            ordenavel: true,
            render: (m) => (m.contentType || "—").replace("image/", "").toUpperCase(),
        },
        {
            chave: COL.tamanhoBytes,
            titulo: "Peso",
            ordenavel: true,
            alinhamento: "direita",
            render: (m) => <span className="preco">{formatarBytes(m.tamanhoBytes)}</span>,
        },
        {
            chave: COL.dataCriacao,
            titulo: "Enviada em",
            ordenavel: true,
            render: (m) => formatarDataHora(m.dataCriacao),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (m) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao(m)}>
                        <FiEdit2 size={13} aria-hidden="true" />
                        Descrever
                    </Botao>
                    <Botao tamanho="sm" variante="texto" onClick={() => setConfirmar(m)}>
                        <FiTrash2 size={13} aria-hidden="true" />
                        Excluir
                    </Botao>
                </div>
            ),
        },
    ];

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobrancelha="Catálogo"
                titulo="Acervo de imagens"
                descricao="As fotos ficam aqui e são vinculadas às peças, às categorias e às coleções. A mesma imagem pode servir a mais de um lugar."
            />

            {/* --------------------------------------------------- Envio */}
            <section className="mb-10 border border-sand bg-linen/50 p-4 sm:p-6">
                <h2 className="mb-1 font-display text-xl tracking-tight text-ink">
                    Enviar imagem
                </h2>
                <p className="mb-5 text-sm leading-relaxed text-ink-soft">
                    JPEG, PNG, WebP ou AVIF, até {formatarBytes(TAMANHO_MAXIMO_BYTES)}.
                </p>

                <div className="grid grid-cols-1 gap-6 lg:grid-cols-[10rem,1fr]">
                    <div className="aspect-product w-full max-w-[10rem] border border-sand bg-base-100">
                        {previa ? (
                            <img
                                src={previa}
                                alt="Pré-visualização da imagem escolhida"
                                className="h-full w-full object-cover"
                            />
                        ) : (
                            <div className="flex h-full w-full items-center justify-center text-taupe">
                                <FiImage size={26} aria-hidden="true" />
                            </div>
                        )}
                    </div>

                    <div className="flex flex-col gap-4">
                        <div className="flex flex-col gap-1.5">
                            <label htmlFor="arquivo-acervo" className="eyebrow">
                                Arquivo
                            </label>
                            <input
                                id="arquivo-acervo"
                                ref={inputRef}
                                type="file"
                                accept={FORMATOS_ACEITOS}
                                onChange={escolher}
                                className="w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-sm text-ink file:mr-4 file:border-0 file:bg-linen file:px-3 file:py-1.5 file:font-sans file:text-xs file:uppercase file:tracking-widest file:text-ink"
                            />
                            {erroArquivo && (
                                <p role="alert" className="text-xs text-danger">
                                    {erroArquivo}
                                </p>
                            )}
                            {arquivo && !erroArquivo && (
                                <p className="text-xs text-ink-soft">
                                    {arquivo.name} · {formatarBytes(arquivo.size)}
                                </p>
                            )}
                        </div>

                        <Campo
                            label="Texto alternativo"
                            value={altTextNovo}
                            maxLength={LIMITES.altText}
                            placeholder="Vestido midi em linho, cor terracota, de frente"
                            ajuda="Descreve a foto para quem usa leitor de tela e para o buscador."
                            onChange={(e) => setAltTextNovo(e.target.value)}
                        />

                        <div className="flex flex-wrap gap-2">
                            <Botao
                                onClick={enviarArquivo}
                                disabled={!arquivo || enviar.isPending}
                                carregando={enviar.isPending}
                            >
                                <FiUpload size={14} aria-hidden="true" />
                                Enviar
                            </Botao>
                            {arquivo && (
                                <Botao
                                    variante="texto"
                                    onClick={limparEnvio}
                                    disabled={enviar.isPending}
                                >
                                    Descartar
                                </Botao>
                            )}
                        </div>
                    </div>
                </div>
            </section>

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar o acervo"
                    acoes={
                        <Botao variante="contorno" tamanho="sm" onClick={() => refetch()}>
                            Tentar de novo
                        </Botao>
                    }
                >
                    <p>A lista não chegou do servidor. Tente novamente em alguns instantes.</p>
                </Aviso>
            ) : (
                <>
                    <FaixaKpis itens={kpis} carregando={isLoading} />

                    <FiltroBusca
                        valor={lista.filtros.q}
                        onBuscar={(texto) => lista.definirFiltro("q", texto)}
                        rotulo="Filtrar imagem"
                        placeholder="Texto alternativo ou endereço"
                        escopo="local"
                        tamanhoPagina={lista.tamanhoPagina}
                        onTamanhoPagina={lista.definirTamanhoPagina}
                        onLimpar={lista.limpar}
                    />

                    {isLoading && (
                        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                            {Array.from({ length: 8 }).map((_, i) => (
                                <Skeleton key={`sk-${i}`} className="aspect-product w-full" />
                            ))}
                        </div>
                    )}

                    {!isLoading && dados.length === 0 && (
                        <EstadoVazio
                            titulo={
                                total === 0 ? "O acervo está vazio" : "Nenhuma imagem com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "Envie as fotos aqui uma vez e use a mesma imagem em quantas peças precisar. Foto de moda é retrato: a vitrine recorta em 3:4."
                                    : "Nenhuma imagem desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                        />
                    )}

                    {!isLoading && dados.length > 0 && (
                        <>
                            {/* Mobile: grade de miniaturas */}
                            <ul className="grid grid-cols-2 gap-4 sm:hidden">
                                {dados.map((m) => (
                                    <li key={m.id} className="border border-sand bg-base-100">
                                        <div className="aspect-product w-full bg-linen">
                                            <img
                                                src={m.url}
                                                alt={m.altText || "Imagem do acervo"}
                                                loading="lazy"
                                                className="h-full w-full object-cover"
                                            />
                                        </div>
                                        <div className="flex flex-col gap-2 p-3">
                                            <p className="truncate font-sans text-xs text-ink-soft">
                                                {m.altText || "Sem texto alternativo"}
                                            </p>
                                            <p className="preco text-xs text-taupe">
                                                {formatarBytes(m.tamanhoBytes)}
                                            </p>
                                            <div className="flex flex-wrap gap-2 border-t border-sand pt-2">
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao(m)}
                                                >
                                                    Descrever
                                                </Botao>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="texto"
                                                    onClick={() => setConfirmar(m)}
                                                >
                                                    Excluir
                                                </Botao>
                                            </div>
                                        </div>
                                    </li>
                                ))}
                            </ul>

                            {/* Desktop: tabela */}
                            <div className="hidden sm:block">
                                <Tabela
                                    colunas={colunas}
                                    dados={dados}
                                    ordenacao={ordenacao}
                                    onOrdenar={ordenar}
                                />
                            </div>

                            <Paginacao
                                className="mt-6"
                                paginaAtual={lista.pagina}
                                totalPaginas={totalPaginas}
                                totalItens={total}
                                itensPorPagina={lista.tamanhoPagina}
                                onMudarPagina={lista.setPagina}
                            />
                        </>
                    )}
                </>
            )}

            <Modal
                isOpen={!!edicao}
                onClose={() => setEdicao(null)}
                titulo="Descrever a imagem"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="form-midia"
                            type="submit"
                            carregando={atualizarAltText.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form id="form-midia" onSubmit={salvarAltText} className="flex flex-col gap-5">
                    {edicao && (
                        <div className="aspect-product w-40 border border-sand bg-linen">
                            <img
                                src={edicao.url}
                                alt={edicao.altText || "Imagem do acervo"}
                                className="h-full w-full object-cover"
                            />
                        </div>
                    )}

                    <Campo
                        label="Texto alternativo"
                        como="textarea"
                        rows={3}
                        value={altTextEdicao}
                        maxLength={LIMITES.altText}
                        ajuda="Só a descrição é editável. Para trocar a foto, envie outra imagem: mudar o arquivo alteraria a peça de todo mundo que usa esta."
                        onChange={(e) => setAltTextEdicao(e.target.value)}
                    />
                </form>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmar}
                titulo="Excluir a imagem do acervo"
                mensagem="A imagem sai do acervo. Se ela estiver vinculada a alguma peça, categoria ou coleção, o servidor recusa a exclusão — desvincule primeiro e tente de novo."
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
