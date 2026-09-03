import { useEffect, useMemo, useState } from "react";
import { FiEdit2, FiPlus, FiTrash2 } from "react-icons/fi";

import Badge from "@/components/ui/Badge.jsx";
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
import FiltroBusca from "@/components/admin/FiltroBusca.jsx";
import LinhaTabela from "@/components/admin/LinhaTabela.jsx";
import { CAMPO, COL } from "@/components/admin/chaves.js";

import { useListaAdmin } from "@/hooks/admin/useListaAdmin.js";
import { useOrdenacaoLocal } from "@/hooks/admin/useOrdenacaoLocal.js";
import {
    useArvoreCategorias,
    useCategoriasAdmin,
    useMutacoesCategoria,
} from "@/hooks/admin/useCategoriasAdmin.js";
import { useMidiasAdmin } from "@/hooks/admin/useMidiasAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { LIMITES } from "@/lib/dominioCatalogo.js";

/**
 * Categorias: a taxonomia da loja, com auto-relação de UM nível
 * ("Vestidos" > "Midi").
 *
 * A paginação é server-side. A busca NÃO é: `CategoriasAdminController` herda o
 * CRUD genérico, que aceita apenas `page` e `pageSize`. A tela diz isso em vez
 * de fingir um `?q=` que o backend ignoraria.
 */
const FORM_VAZIO = {
    nome: "",
    slug: "",
    descricao: "",
    idCategoriaPai: "",
    idMidiaCapa: "",
    ordem: "0",
    habilitado: true,
    metaTitle: "",
    metaDescription: "",
};

function paraForm(categoria) {
    return {
        nome: categoria.nome ?? "",
        slug: categoria.slug ?? "",
        descricao: categoria.descricao ?? "",
        idCategoriaPai: categoria.idCategoriaPai ?? "",
        idMidiaCapa: categoria.idMidiaCapa ?? "",
        ordem: String(categoria.ordem ?? 0),
        habilitado: !!categoria.habilitado,
        metaTitle: categoria.metaTitle ?? "",
        metaDescription: categoria.metaDescription ?? "",
    };
}

export default function Categorias() {
    const toast = useToast();

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useCategoriasAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { opcoes: todasCategorias } = useArvoreCategorias(false);
    const { itens: midias } = useMidiasAdmin({ pagina: 1, tamanhoPagina: 100 });
    const { criar, atualizar, remover } = useMutacoesCategoria();

    const [edicao, setEdicao] = useState(null); // { categoria | null }
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!edicao) return;
        setForm(edicao.categoria ? paraForm(edicao.categoria) : FORM_VAZIO);
        setErros({});
    }, [edicao]);

    const filtradas = useMemo(() => {
        const termo = lista.filtros.q.trim().toLowerCase();
        if (!termo) return itens;
        return itens.filter(
            (c) =>
                c.nome.toLowerCase().includes(termo) || (c.slug ?? "").toLowerCase().includes(termo),
        );
    }, [itens, lista.filtros.q]);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(filtradas);

    const nomePai = (idPai) =>
        todasCategorias.find((c) => c.id === idPai)?.nome ?? (idPai ? `#${idPai}` : "—");

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const salvar = async (evento) => {
        evento.preventDefault();

        const encontrados = {};
        if (form.nome.trim().length < 2)
            encontrados.nome = "O nome deve ter entre 2 e 180 caracteres.";
        setErros(encontrados);
        if (Object.keys(encontrados).length > 0) return;

        const payload = {
            nome: form.nome.trim(),
            slug: form.slug.trim() || null,
            descricao: form.descricao.trim() || null,
            idCategoriaPai: form.idCategoriaPai === "" ? null : Number(form.idCategoriaPai),
            idMidiaCapa: form.idMidiaCapa === "" ? null : Number(form.idMidiaCapa),
            ordem: Number(form.ordem) || 0,
            habilitado: form.habilitado,
            metaTitle: form.metaTitle.trim() || null,
            metaDescription: form.metaDescription.trim() || null,
        };

        try {
            if (edicao.categoria) {
                await atualizar.mutateAsync({ id: edicao.categoria.id, payload });
                toast.success("Categoria salva.");
            } else {
                await criar.mutateAsync(payload);
                toast.success("Categoria criada.");
            }
            setEdicao(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const excluir = async () => {
        try {
            await remover.mutateAsync(confirmar.id);
            toast.success(`"${confirmar.nome}" foi excluída.`);
            setConfirmar(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const colunas = [
        {
            chave: COL.nome,
            titulo: "Categoria",
            ordenavel: true,
            render: (c) => (
                <div className="min-w-0">
                    <p className="truncate font-sans text-sm text-ink">{c.nome}</p>
                    <span className="text-xs text-taupe">{c.slug}</span>
                </div>
            ),
        },
        {
            chave: COL.slug,
            titulo: "Dentro de",
            render: (c) => nomePai(c.idCategoriaPai),
        },
        {
            chave: COL.ordem,
            titulo: "Ordem",
            ordenavel: true,
            alinhamento: "direita",
            render: (c) => <span className="preco">{c.ordem}</span>,
        },
        {
            chave: COL.habilitado,
            titulo: "Situação",
            ordenavel: true,
            render: (c) => (
                <Badge variante={c.habilitado ? "neutro" : "esgotado"}>
                    {c.habilitado ? "Visível" : "Oculta"}
                </Badge>
            ),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (c) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao({ categoria: c })}>
                        <FiEdit2 size={13} aria-hidden="true" />
                        Editar
                    </Botao>
                    <Botao tamanho="sm" variante="texto" onClick={() => setConfirmar(c)}>
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
                titulo="Categorias"
                descricao="A taxonomia da loja, com um nível de subcategoria. A ordem define como o menu se apresenta."
                acoes={
                    <Botao onClick={() => setEdicao({ categoria: null })}>
                        <FiPlus size={14} aria-hidden="true" />
                        Nova categoria
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar as categorias"
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
                    <FiltroBusca
                        valor={lista.filtros.q}
                        onBuscar={(texto) => lista.definirFiltro("q", texto)}
                        rotulo="Filtrar categoria"
                        placeholder="Nome ou endereço"
                        escopo="local"
                        tamanhoPagina={lista.tamanhoPagina}
                        onTamanhoPagina={lista.definirTamanhoPagina}
                        onLimpar={lista.limpar}
                    />

                    {isLoading && (
                        <div className="flex flex-col gap-3">
                            {Array.from({ length: 5 }).map((_, i) => (
                                <Skeleton key={`sk-${i}`} className="h-14 w-full" />
                            ))}
                        </div>
                    )}

                    {!isLoading && dados.length === 0 && (
                        <EstadoVazio
                            titulo={
                                total === 0
                                    ? "Nenhuma categoria cadastrada"
                                    : "Nenhuma categoria com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "A categoria é o primeiro passo do catálogo: toda peça precisa pertencer a uma. Comece pelas grandes famílias, como Vestidos e Blusas, e crie as subcategorias depois."
                                    : "Nenhuma categoria desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                            acao={
                                total === 0 ? (
                                    <Botao onClick={() => setEdicao({ categoria: null })}>
                                        <FiPlus size={14} aria-hidden="true" />
                                        Criar categoria
                                    </Botao>
                                ) : (
                                    <Botao variante="contorno" onClick={lista.limpar}>
                                        Limpar filtro
                                    </Botao>
                                )
                            }
                        />
                    )}

                    {!isLoading && dados.length > 0 && (
                        <>
                            <div className="flex flex-col gap-3 sm:hidden">
                                {dados.map((c) => (
                                    <LinhaTabela
                                        key={c.id}
                                        titulo={c.nome}
                                        subtitulo={c.slug}
                                        selo={
                                            <Badge variante={c.habilitado ? "neutro" : "esgotado"}>
                                                {c.habilitado ? "Visível" : "Oculta"}
                                            </Badge>
                                        }
                                        campos={[
                                            { rotulo: "Dentro de", valor: nomePai(c.idCategoriaPai) },
                                            { rotulo: "Ordem", valor: c.ordem },
                                        ]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao({ categoria: c })}
                                                >
                                                    Editar
                                                </Botao>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="texto"
                                                    onClick={() => setConfirmar(c)}
                                                >
                                                    Excluir
                                                </Botao>
                                            </>
                                        }
                                    />
                                ))}
                            </div>

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

            {/* ------------------------------------------------ Formulário */}
            <Modal
                isOpen={!!edicao}
                onClose={() => setEdicao(null)}
                largura="lg"
                titulo={edicao?.categoria ? "Editar categoria" : "Nova categoria"}
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="form-categoria"
                            type="submit"
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form
                    id="form-categoria"
                    onSubmit={salvar}
                    noValidate
                    className="flex flex-col gap-5"
                >
                    <Campo
                        label="Nome"
                        obrigatorio
                        value={form.nome}
                        erro={erros.nome}
                        maxLength={LIMITES.categoriaNome}
                        onChange={(e) => setCampo("nome", e.target.value)}
                    />

                    <Campo
                        label="Endereço na loja"
                        value={form.slug}
                        maxLength={LIMITES.produtoSlug}
                        ajuda={
                            edicao?.categoria
                                ? "Em branco, mantém o endereço atual. Trocar quebra o link já indexado."
                                : "Em branco, o endereço é gerado a partir do nome."
                        }
                        onChange={(e) => setCampo("slug", e.target.value)}
                    />

                    <Campo
                        label="Descrição"
                        como="textarea"
                        rows={3}
                        value={form.descricao}
                        onChange={(e) => setCampo(CAMPO.descricao, e.target.value)}
                    />

                    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                        <Campo
                            label="Dentro de"
                            como="select"
                            value={form.idCategoriaPai}
                            ajuda="A hierarquia tem um nível só."
                            onChange={(e) => setCampo("idCategoriaPai", e.target.value)}
                        >
                            <option value="">Categoria principal</option>
                            {todasCategorias
                                .filter(
                                    (c) =>
                                        c.profundidade === 0 && c.id !== edicao?.categoria?.id,
                                )
                                .map((c) => (
                                    <option key={c.id} value={c.id}>
                                        {c.nome}
                                    </option>
                                ))}
                        </Campo>

                        <Campo
                            label="Ordem no menu"
                            inputMode="numeric"
                            value={form.ordem}
                            ajuda="Menor primeiro."
                            onChange={(e) => setCampo("ordem", e.target.value.replace(/\D/g, ""))}
                        />
                    </div>

                    <Campo
                        label="Imagem de capa"
                        como="select"
                        value={form.idMidiaCapa}
                        ajuda="Escolha entre as imagens já enviadas para o acervo."
                        onChange={(e) => setCampo("idMidiaCapa", e.target.value)}
                    >
                        <option value="">Sem capa</option>
                        {midias.map((midia) => (
                            <option key={midia.id} value={midia.id}>
                                {midia.altText || midia.url}
                            </option>
                        ))}
                    </Campo>

                    <Campo
                        label="Título para o buscador"
                        value={form.metaTitle}
                        maxLength={LIMITES.metaTitle}
                        onChange={(e) => setCampo("metaTitle", e.target.value)}
                    />

                    <Campo
                        label="Resumo para o buscador"
                        como="textarea"
                        rows={2}
                        value={form.metaDescription}
                        maxLength={LIMITES.metaDescription}
                        onChange={(e) => setCampo("metaDescription", e.target.value)}
                    />

                    <label className="flex items-center gap-2 font-sans text-sm text-ink">
                        <input
                            type="checkbox"
                            checked={form.habilitado}
                            onChange={(e) => setCampo("habilitado", e.target.checked)}
                            className="h-4 w-4 accent-olive"
                        />
                        Visível na loja
                    </label>
                </form>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmar}
                titulo="Excluir a categoria"
                mensagem={`"${confirmar?.nome}" será excluída. Categorias com peças ou subcategorias vinculadas são recusadas pelo servidor — desmarque "visível na loja" quando quiser apenas escondê-la.`}
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
