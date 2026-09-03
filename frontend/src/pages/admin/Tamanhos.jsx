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
import { useMutacoesTamanho, useTamanhosAdmin } from "@/hooks/admin/useTamanhosAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { GRADES_TAMANHO, GRADE_TAMANHO, LIMITES, rotuloGrade } from "@/lib/dominioCatalogo.js";

/**
 * Grade de tamanhos.
 *
 * A coluna "ordem" é o motivo de esta tela existir: sem ela o seletor da loja
 * sairia em ordem alfabética e "GG" viria antes de "P".
 */
const FORM_VAZIO = {
    codigo: "",
    descricao: "",
    ordem: "0",
    grade: GRADE_TAMANHO.ALFA,
    ativo: true,
};

function paraForm(tamanho) {
    return {
        codigo: tamanho.codigo ?? "",
        descricao: tamanho.descricao ?? "",
        ordem: String(tamanho.ordem ?? 0),
        grade: tamanho.grade ?? GRADE_TAMANHO.ALFA,
        ativo: !!tamanho.ativo,
    };
}

export default function Tamanhos() {
    const toast = useToast();

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useTamanhosAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { criar, atualizar, remover } = useMutacoesTamanho();

    const [edicao, setEdicao] = useState(null);
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!edicao) return;
        setForm(edicao.tamanho ? paraForm(edicao.tamanho) : FORM_VAZIO);
        setErros({});
    }, [edicao]);

    const filtrados = useMemo(() => {
        const termo = lista.filtros.q.trim().toLowerCase();
        if (!termo) return itens;
        return itens.filter(
            (t) =>
                t.codigo.toLowerCase().includes(termo) ||
                (t.descricao ?? "").toLowerCase().includes(termo),
        );
    }, [itens, lista.filtros.q]);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(filtrados);

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const salvar = async (evento) => {
        evento.preventDefault();

        const encontrados = {};
        if (form.codigo.trim().length < 1)
            encontrados.codigo = "Informe o código do tamanho (de 1 a 10 caracteres).";
        setErros(encontrados);
        if (Object.keys(encontrados).length > 0) return;

        const payload = {
            codigo: form.codigo.trim(),
            descricao: form.descricao.trim() || null,
            ordem: Number(form.ordem) || 0,
            grade: Number(form.grade),
            ativo: form.ativo,
        };

        try {
            if (edicao.tamanho) {
                await atualizar.mutateAsync({ id: edicao.tamanho.id, payload });
                toast.success("Tamanho salvo.");
            } else {
                await criar.mutateAsync(payload);
                toast.success("Tamanho criado.");
            }
            setEdicao(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const excluir = async () => {
        try {
            await remover.mutateAsync(confirmar.id);
            toast.success(`O tamanho ${confirmar.codigo} foi excluído.`);
            setConfirmar(null);
        } catch {
            /* o interceptor do axios já mostrou o erro */
        }
    };

    const colunas = [
        {
            chave: COL.codigo,
            titulo: "Código",
            ordenavel: true,
            render: (t) => <span className="font-sans text-sm uppercase text-ink">{t.codigo}</span>,
        },
        {
            chave: COL.descricao,
            titulo: "Descrição",
            render: (t) => t.descricao || "—",
        },
        {
            chave: COL.grade,
            titulo: "Grade",
            ordenavel: true,
            render: (t) => rotuloGrade(t.grade),
        },
        {
            chave: COL.ordem,
            titulo: "Ordem",
            ordenavel: true,
            alinhamento: "direita",
            render: (t) => <span className="preco">{t.ordem}</span>,
        },
        {
            chave: COL.ativo,
            titulo: "Situação",
            ordenavel: true,
            render: (t) => (
                <Badge variante={t.ativo ? "neutro" : "esgotado"}>
                    {t.ativo ? "Em uso" : "Fora de uso"}
                </Badge>
            ),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (t) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao({ tamanho: t })}>
                        <FiEdit2 size={13} aria-hidden="true" />
                        Editar
                    </Botao>
                    <Botao tamanho="sm" variante="texto" onClick={() => setConfirmar(t)}>
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
                titulo="Tamanhos"
                descricao="A grade que alimenta a matriz de variações. A ordem define como o seletor aparece para o cliente."
                acoes={
                    <Botao onClick={() => setEdicao({ tamanho: null })}>
                        <FiPlus size={14} aria-hidden="true" />
                        Novo tamanho
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar os tamanhos"
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
                        rotulo="Filtrar tamanho"
                        placeholder="Código ou descrição"
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
                                    ? "Nenhum tamanho cadastrado"
                                    : "Nenhum tamanho com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "Sem tamanhos não há matriz de variações. Comece pela grade alfabética (PP, P, M, G, GG) e use o campo de ordem para deixá-la na sequência certa."
                                    : "Nenhum tamanho desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                            acao={
                                total === 0 ? (
                                    <Botao onClick={() => setEdicao({ tamanho: null })}>
                                        <FiPlus size={14} aria-hidden="true" />
                                        Criar tamanho
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
                                {dados.map((t) => (
                                    <LinhaTabela
                                        key={t.id}
                                        titulo={t.codigo}
                                        subtitulo={t.descricao || rotuloGrade(t.grade)}
                                        selo={
                                            <Badge variante={t.ativo ? "neutro" : "esgotado"}>
                                                {t.ativo ? "Em uso" : "Fora de uso"}
                                            </Badge>
                                        }
                                        campos={[
                                            { rotulo: "Grade", valor: rotuloGrade(t.grade) },
                                            { rotulo: "Ordem", valor: t.ordem },
                                        ]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao({ tamanho: t })}
                                                >
                                                    Editar
                                                </Botao>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="texto"
                                                    onClick={() => setConfirmar(t)}
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

            <Modal
                isOpen={!!edicao}
                onClose={() => setEdicao(null)}
                titulo={edicao?.tamanho ? "Editar tamanho" : "Novo tamanho"}
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="form-tamanho"
                            type="submit"
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form id="form-tamanho" onSubmit={salvar} noValidate className="flex flex-col gap-5">
                    <Campo
                        label="Código"
                        obrigatorio
                        value={form.codigo}
                        erro={erros.codigo}
                        maxLength={LIMITES.tamanhoCodigo}
                        placeholder="M"
                        ajuda="É o que aparece no seletor da loja."
                        onChange={(e) => setCampo(CAMPO.codigo, e.target.value)}
                    />

                    <Campo
                        label="Descrição"
                        value={form.descricao}
                        maxLength={LIMITES.tamanhoDescricao}
                        placeholder="Veste 40 a 42"
                        onChange={(e) => setCampo(CAMPO.descricao, e.target.value)}
                    />

                    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                        <Campo
                            label="Grade"
                            como="select"
                            value={form.grade}
                            ajuda="Separa a numeração de calça da grade alfabética."
                            onChange={(e) => setCampo("grade", e.target.value)}
                        >
                            {GRADES_TAMANHO.map(({ valor, rotulo }) => (
                                <option key={valor} value={valor}>
                                    {rotulo}
                                </option>
                            ))}
                        </Campo>

                        <Campo
                            label="Ordem"
                            inputMode="numeric"
                            value={form.ordem}
                            ajuda="Menor primeiro: PP antes de P, P antes de M."
                            onChange={(e) => setCampo("ordem", e.target.value.replace(/\D/g, ""))}
                        />
                    </div>

                    <label className="flex items-center gap-2 font-sans text-sm text-ink">
                        <input
                            type="checkbox"
                            checked={form.ativo}
                            onChange={(e) => setCampo("ativo", e.target.checked)}
                            className="h-4 w-4 accent-olive"
                        />
                        Disponível para novas variações
                    </label>
                </form>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmar}
                titulo="Excluir o tamanho"
                mensagem={`O tamanho ${confirmar?.codigo} será excluído. Tamanhos já usados em alguma variação são recusados pelo servidor — nesse caso, desmarque "disponível para novas variações" para tirá-lo de circulação sem apagar nada.`}
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
