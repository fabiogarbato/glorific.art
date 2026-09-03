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
import { COL } from "@/components/admin/chaves.js";

import { useListaAdmin } from "@/hooks/admin/useListaAdmin.js";
import { useOrdenacaoLocal } from "@/hooks/admin/useOrdenacaoLocal.js";
import {
    useMutacoesTabelaMedidas,
    useTabelasMedidasAdmin,
} from "@/hooks/admin/useTabelasMedidasAdmin.js";
import { useTamanhosAtivos } from "@/hooks/admin/useTamanhosAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { LIMITES } from "@/lib/dominioCatalogo.js";

/**
 * Guias de medidas — o dado que mais reduz devolução por tamanho errado.
 *
 * Regra do backend que molda o formulário: as linhas enviadas SUBSTITUEM as
 * atuais em bloco. Não existe edição de linha isolada, então a tela sempre
 * reenvia a tabela inteira, e remover um tamanho da grade não deixa linha órfã.
 */
const MEDIDAS = [
    { campo: "bustoCm", rotulo: "Busto" },
    { campo: "cinturaCm", rotulo: "Cintura" },
    { campo: "quadrilCm", rotulo: "Quadril" },
    { campo: "comprimentoCm", rotulo: "Comprimento" },
    { campo: "mangaCm", rotulo: "Manga" },
];

const LINHA_VAZIA = {
    idTamanho: "",
    bustoCm: "",
    cinturaCm: "",
    quadrilCm: "",
    comprimentoCm: "",
    mangaCm: "",
};

const FORM_VAZIO = { nome: "", observacao: "", ativo: true, linhas: [] };

/** Campo vazio vira null: o DTO aceita medida ausente, mas não string. */
function paraMedida(texto) {
    if (texto === "" || texto === null || texto === undefined) return null;
    const numero = Number(String(texto).replace(",", "."));
    return Number.isFinite(numero) ? numero : null;
}

function paraForm(tabela) {
    return {
        nome: tabela.nome ?? "",
        observacao: tabela.observacao ?? "",
        ativo: !!tabela.ativo,
        linhas: (tabela.linhas ?? []).map((linha) => ({
            idTamanho: linha.idTamanho ?? "",
            bustoCm: linha.bustoCm ?? "",
            cinturaCm: linha.cinturaCm ?? "",
            quadrilCm: linha.quadrilCm ?? "",
            comprimentoCm: linha.comprimentoCm ?? "",
            mangaCm: linha.mangaCm ?? "",
        })),
    };
}

export default function TabelasMedidas() {
    const toast = useToast();

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useTabelasMedidasAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { tamanhos } = useTamanhosAtivos();
    const { criar, atualizar, remover } = useMutacoesTabelaMedidas();

    const [edicao, setEdicao] = useState(null);
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!edicao) return;
        setForm(edicao.tabela ? paraForm(edicao.tabela) : FORM_VAZIO);
        setErros({});
    }, [edicao]);

    const filtradas = useMemo(() => {
        const termo = lista.filtros.q.trim().toLowerCase();
        if (!termo) return itens;
        return itens.filter((t) => t.nome.toLowerCase().includes(termo));
    }, [itens, lista.filtros.q]);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(filtradas);

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const editarLinha = (indice, campo, valor) =>
        setForm((atual) => ({
            ...atual,
            linhas: atual.linhas.map((linha, i) =>
                i === indice ? { ...linha, [campo]: valor } : linha,
            ),
        }));

    const adicionarLinha = () =>
        setForm((atual) => ({ ...atual, linhas: [...atual.linhas, { ...LINHA_VAZIA }] }));

    const removerLinha = (indice) =>
        setForm((atual) => ({
            ...atual,
            linhas: atual.linhas.filter((_, i) => i !== indice),
        }));

    const nomeTamanho = (idTamanho) =>
        tamanhos.find((t) => t.id === Number(idTamanho))?.codigo ?? "—";

    const salvar = async (evento) => {
        evento.preventDefault();

        const encontrados = {};
        if (form.nome.trim().length < 2)
            encontrados.nome = "O nome deve ter entre 2 e 120 caracteres.";
        if (form.linhas.length === 0)
            encontrados.linhas =
                "Uma guia sem linha não serve para nada. Acrescente ao menos um tamanho.";
        else if (form.linhas.some((linha) => !linha.idTamanho))
            encontrados.linhas = "Escolha o tamanho de cada linha.";
        else {
            const ids = form.linhas.map((linha) => Number(linha.idTamanho));
            if (new Set(ids).size !== ids.length)
                encontrados.linhas = "O mesmo tamanho aparece em duas linhas.";
        }

        setErros(encontrados);
        if (Object.keys(encontrados).length > 0) return;

        const payload = {
            nome: form.nome.trim(),
            observacao: form.observacao.trim() || null,
            ativo: form.ativo,
            linhas: form.linhas.map((linha, indice) => ({
                idTamanho: Number(linha.idTamanho),
                bustoCm: paraMedida(linha.bustoCm),
                cinturaCm: paraMedida(linha.cinturaCm),
                quadrilCm: paraMedida(linha.quadrilCm),
                comprimentoCm: paraMedida(linha.comprimentoCm),
                mangaCm: paraMedida(linha.mangaCm),
                ordem: indice,
            })),
        };

        try {
            if (edicao.tabela) {
                await atualizar.mutateAsync({ id: edicao.tabela.id, payload });
                toast.success("Guia de medidas salva.");
            } else {
                await criar.mutateAsync(payload);
                toast.success("Guia de medidas criada.");
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
        { chave: COL.nome, titulo: "Guia", ordenavel: true },
        {
            chave: COL.linhas,
            titulo: "Tamanhos",
            alinhamento: "direita",
            render: (t) => <span className="preco">{t.linhas?.length ?? 0}</span>,
        },
        {
            chave: COL.observacao,
            titulo: "Observação",
            render: (t) => (
                <span className="block max-w-[24rem] truncate">{t.observacao || "—"}</span>
            ),
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
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao({ tabela: t })}>
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
                titulo="Guias de medidas"
                descricao="As medidas do corpo por tamanho. É o dado que mais reduz devolução por tamanho errado."
                acoes={
                    <Botao onClick={() => setEdicao({ tabela: null })}>
                        <FiPlus size={14} aria-hidden="true" />
                        Nova guia
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar as guias de medidas"
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
                        rotulo="Filtrar guia"
                        placeholder="Nome da guia"
                        escopo="local"
                        tamanhoPagina={lista.tamanhoPagina}
                        onTamanhoPagina={lista.definirTamanhoPagina}
                        onLimpar={lista.limpar}
                    />

                    {isLoading && (
                        <div className="flex flex-col gap-3">
                            {Array.from({ length: 4 }).map((_, i) => (
                                <Skeleton key={`sk-${i}`} className="h-14 w-full" />
                            ))}
                        </div>
                    )}

                    {!isLoading && dados.length === 0 && (
                        <EstadoVazio
                            titulo={
                                total === 0
                                    ? "Nenhuma guia de medidas"
                                    : "Nenhuma guia com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "Uma guia lista busto, cintura, quadril, comprimento e manga para cada tamanho. Crie uma por tipo de modelagem e vincule no formulário da peça."
                                    : "Nenhuma guia desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                            acao={
                                total === 0 ? (
                                    <Botao onClick={() => setEdicao({ tabela: null })}>
                                        <FiPlus size={14} aria-hidden="true" />
                                        Criar guia
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
                                        titulo={t.nome}
                                        subtitulo={t.observacao || undefined}
                                        selo={
                                            <Badge variante={t.ativo ? "neutro" : "esgotado"}>
                                                {t.ativo ? "Em uso" : "Fora de uso"}
                                            </Badge>
                                        }
                                        campos={[
                                            { rotulo: "Tamanhos", valor: t.linhas?.length ?? 0 },
                                        ]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao({ tabela: t })}
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
                largura="xl"
                titulo={edicao?.tabela ? "Editar guia de medidas" : "Nova guia de medidas"}
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="form-tabela-medidas"
                            type="submit"
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form
                    id="form-tabela-medidas"
                    onSubmit={salvar}
                    noValidate
                    className="flex flex-col gap-5"
                >
                    <Campo
                        label="Nome"
                        obrigatorio
                        value={form.nome}
                        erro={erros.nome}
                        maxLength={LIMITES.tabelaNome}
                        placeholder="Vestidos de modelagem reta"
                        onChange={(e) => setCampo("nome", e.target.value)}
                    />

                    <Campo
                        label="Observação"
                        como="textarea"
                        rows={2}
                        value={form.observacao}
                        placeholder="Medidas do corpo, em centímetros, e não da peça pronta."
                        onChange={(e) => setCampo("observacao", e.target.value)}
                    />

                    <div className="flex flex-col gap-3">
                        <div className="flex flex-wrap items-center justify-between gap-3">
                            <h3 className="font-display text-lg tracking-tight text-ink">
                                Linhas da guia
                            </h3>
                            <Botao
                                tamanho="sm"
                                variante="sutil"
                                onClick={adicionarLinha}
                                disabled={tamanhos.length === 0}
                            >
                                <FiPlus size={13} aria-hidden="true" />
                                Acrescentar tamanho
                            </Botao>
                        </div>

                        {tamanhos.length === 0 && (
                            <Aviso variante="alerta">
                                <p>
                                    Nenhum tamanho ativo cadastrado. Crie a grade de tamanhos antes
                                    de montar a guia de medidas.
                                </p>
                            </Aviso>
                        )}

                        {erros.linhas && (
                            <p role="alert" className="text-xs text-danger">
                                {erros.linhas}
                            </p>
                        )}

                        {form.linhas.length === 0 && tamanhos.length > 0 && (
                            <p className="border border-dashed border-sand px-4 py-8 text-center text-sm text-ink-soft">
                                Acrescente um tamanho para começar a preencher as medidas.
                            </p>
                        )}

                        {form.linhas.map((linha, indice) => (
                            <div
                                key={`linha-${indice}`}
                                className="border border-sand bg-linen/40 p-4"
                            >
                                <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
                                    <Campo
                                        label="Tamanho"
                                        como="select"
                                        obrigatorio
                                        containerClassName="w-40"
                                        value={linha.idTamanho}
                                        onChange={(e) =>
                                            editarLinha(indice, "idTamanho", e.target.value)
                                        }
                                    >
                                        <option value="">Escolher</option>
                                        {tamanhos.map((tamanho) => (
                                            <option key={tamanho.id} value={tamanho.id}>
                                                {tamanho.codigo}
                                            </option>
                                        ))}
                                    </Campo>

                                    <button
                                        type="button"
                                        onClick={() => removerLinha(indice)}
                                        aria-label={`Remover a linha do tamanho ${nomeTamanho(linha.idTamanho)}`}
                                        className="flex h-11 items-center gap-2 border border-sand px-3 font-sans text-xs uppercase tracking-widest text-ink-soft transition-colors hover:border-danger hover:text-danger"
                                    >
                                        <FiTrash2 size={13} aria-hidden="true" />
                                        Remover
                                    </button>
                                </div>

                                <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
                                    {MEDIDAS.map(({ campo, rotulo }) => (
                                        <Campo
                                            key={campo}
                                            label={`${rotulo} (cm)`}
                                            inputMode="decimal"
                                            value={linha[campo]}
                                            onChange={(e) =>
                                                editarLinha(
                                                    indice,
                                                    campo,
                                                    e.target.value.replace(/[^\d.,]/g, ""),
                                                )
                                            }
                                        />
                                    ))}
                                </div>
                            </div>
                        ))}
                    </div>

                    <label className="flex items-center gap-2 font-sans text-sm text-ink">
                        <input
                            type="checkbox"
                            checked={form.ativo}
                            onChange={(e) => setCampo("ativo", e.target.checked)}
                            className="h-4 w-4 accent-olive"
                        />
                        Disponível para vincular a peças
                    </label>
                </form>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmar}
                titulo="Excluir a guia de medidas"
                mensagem={`"${confirmar?.nome}" será excluída, com todas as suas linhas. As peças que apontam para ela ficam sem guia de medidas.`}
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
