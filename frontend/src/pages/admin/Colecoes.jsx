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
import { useColecoesAdmin, useMutacoesColecao } from "@/hooks/admin/useColecoesAdmin.js";
import { useMidiasAdmin } from "@/hooks/admin/useMidiasAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { LIMITES } from "@/lib/dominioCatalogo.js";
import { formatarData, paraInputDate } from "@/utils/datas.js";

/**
 * Coleções (drops): curadoria com vigência, epígrafe e banner.
 *
 * A vitrine de cada drop é montada peça a peça, no formulário do produto — é lá
 * que existe o campo de coleções. Aqui ficam os dados da coleção em si.
 *
 * Datas: o backend guarda vigência em UTC. O `<input type="date">` devolve
 * "aaaa-mm-dd" sem fuso, então a conversão é explícita — sem isso, quem estiver
 * em UTC-3 salvaria o dia anterior.
 */
const FORM_VAZIO = {
    nome: "",
    slug: "",
    descricao: "",
    epigrafe: "",
    idMidiaCapa: "",
    idMidiaBanner: "",
    dataInicio: "",
    dataFim: "",
    destaque: false,
    habilitado: true,
    ordem: "0",
};

function paraForm(colecao) {
    return {
        nome: colecao.nome ?? "",
        slug: colecao.slug ?? "",
        descricao: colecao.descricao ?? "",
        epigrafe: colecao.epigrafe ?? "",
        idMidiaCapa: colecao.idMidiaCapa ?? "",
        idMidiaBanner: colecao.idMidiaBanner ?? "",
        dataInicio: paraInputDate(colecao.dataInicio),
        dataFim: paraInputDate(colecao.dataFim),
        destaque: !!colecao.destaque,
        habilitado: !!colecao.habilitado,
        ordem: String(colecao.ordem ?? 0),
    };
}

/** "aaaa-mm-dd" -> meia-noite UTC daquele dia. Vazio vira null (sem prazo). */
function paraUtc(texto) {
    if (!texto) return null;
    return new Date(`${texto}T00:00:00Z`).toISOString();
}

export default function Colecoes() {
    const toast = useToast();

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useColecoesAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { itens: midias } = useMidiasAdmin({ pagina: 1, tamanhoPagina: 100 });
    const { criar, atualizar, remover } = useMutacoesColecao();

    const [edicao, setEdicao] = useState(null);
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!edicao) return;
        setForm(edicao.colecao ? paraForm(edicao.colecao) : FORM_VAZIO);
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

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const salvar = async (evento) => {
        evento.preventDefault();

        const encontrados = {};
        if (form.nome.trim().length < 2)
            encontrados.nome = "O nome deve ter entre 2 e 180 caracteres.";
        if (form.dataInicio && form.dataFim && form.dataFim < form.dataInicio)
            encontrados.dataFim = "O fim da vigência não pode ser antes do início.";

        setErros(encontrados);
        if (Object.keys(encontrados).length > 0) return;

        const payload = {
            nome: form.nome.trim(),
            slug: form.slug.trim() || null,
            descricao: form.descricao.trim() || null,
            epigrafe: form.epigrafe.trim() || null,
            idMidiaCapa: form.idMidiaCapa === "" ? null : Number(form.idMidiaCapa),
            idMidiaBanner: form.idMidiaBanner === "" ? null : Number(form.idMidiaBanner),
            dataInicio: paraUtc(form.dataInicio),
            dataFim: paraUtc(form.dataFim),
            destaque: form.destaque,
            habilitado: form.habilitado,
            ordem: Number(form.ordem) || 0,
        };

        try {
            if (edicao.colecao) {
                await atualizar.mutateAsync({ id: edicao.colecao.id, payload });
                toast.success("Coleção salva.");
            } else {
                await criar.mutateAsync(payload);
                toast.success("Coleção criada.");
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

    const vigencia = (c) => {
        if (!c.dataInicio && !c.dataFim) return "Sem prazo";
        if (c.dataInicio && !c.dataFim) return `A partir de ${formatarData(c.dataInicio)}`;
        if (!c.dataInicio && c.dataFim) return `Até ${formatarData(c.dataFim)}`;
        return `${formatarData(c.dataInicio)} a ${formatarData(c.dataFim)}`;
    };

    const colunas = [
        {
            chave: COL.nome,
            titulo: "Coleção",
            ordenavel: true,
            render: (c) => (
                <div className="min-w-0">
                    <p className="truncate font-sans text-sm text-ink">{c.nome}</p>
                    <span className="text-xs text-taupe">{c.slug}</span>
                </div>
            ),
        },
        { chave: COL.dataInicio, titulo: "Vigência", render: vigencia },
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
                <div className="flex flex-wrap gap-1">
                    <Badge variante={c.habilitado ? "neutro" : "esgotado"}>
                        {c.habilitado ? "Visível" : "Oculta"}
                    </Badge>
                    {c.destaque && <Badge variante="destaque">Destaque</Badge>}
                </div>
            ),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (c) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao({ colecao: c })}>
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
                titulo="Coleções"
                descricao="Cada coleção é um drop com vigência própria. As peças entram pela aba de coleções do formulário da peça."
                acoes={
                    <Botao onClick={() => setEdicao({ colecao: null })}>
                        <FiPlus size={14} aria-hidden="true" />
                        Nova coleção
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar as coleções"
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
                        rotulo="Filtrar coleção"
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
                                    ? "Nenhuma coleção criada"
                                    : "Nenhuma coleção com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "Uma coleção reúne peças em torno de uma ideia, com epígrafe e vigência. É o que dá ritmo à vitrine ao longo do ano."
                                    : "Nenhuma coleção desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                            acao={
                                total === 0 ? (
                                    <Botao onClick={() => setEdicao({ colecao: null })}>
                                        <FiPlus size={14} aria-hidden="true" />
                                        Criar coleção
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
                                            { rotulo: "Vigência", valor: vigencia(c) },
                                            { rotulo: "Ordem", valor: c.ordem },
                                        ]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao({ colecao: c })}
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

            <Modal
                isOpen={!!edicao}
                onClose={() => setEdicao(null)}
                largura="lg"
                titulo={edicao?.colecao ? "Editar coleção" : "Nova coleção"}
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="formColecao"
                            type="submit"
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form id="formColecao" onSubmit={salvar} noValidate className="flex flex-col gap-5">
                    <Campo
                        label="Nome"
                        obrigatorio
                        value={form.nome}
                        erro={erros.nome}
                        maxLength={LIMITES.colecaoNome}
                        onChange={(e) => setCampo("nome", e.target.value)}
                    />

                    <Campo
                        label="Endereço na loja"
                        value={form.slug}
                        maxLength={LIMITES.produtoSlug}
                        ajuda={
                            edicao?.colecao
                                ? "Em branco, mantém o endereço atual."
                                : "Em branco, o endereço é gerado a partir do nome."
                        }
                        onChange={(e) => setCampo("slug", e.target.value)}
                    />

                    <Campo
                        label="Epígrafe"
                        value={form.epigrafe}
                        maxLength={LIMITES.colecaoEpigrafe}
                        placeholder="Versículo ou frase que abre a coleção"
                        onChange={(e) => setCampo("epigrafe", e.target.value)}
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
                            label="Início da vigência"
                            type="date"
                            value={form.dataInicio}
                            ajuda="Em branco, a coleção já vale."
                            onChange={(e) => setCampo("dataInicio", e.target.value)}
                        />
                        <Campo
                            label="Fim da vigência"
                            type="date"
                            value={form.dataFim}
                            erro={erros.dataFim}
                            ajuda="Em branco, a coleção fica sem prazo."
                            onChange={(e) => setCampo("dataFim", e.target.value)}
                        />
                    </div>

                    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                        <Campo
                            label="Imagem de capa"
                            como="select"
                            value={form.idMidiaCapa}
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
                            label="Imagem de banner"
                            como="select"
                            value={form.idMidiaBanner}
                            onChange={(e) => setCampo("idMidiaBanner", e.target.value)}
                        >
                            <option value="">Sem banner</option>
                            {midias.map((midia) => (
                                <option key={midia.id} value={midia.id}>
                                    {midia.altText || midia.url}
                                </option>
                            ))}
                        </Campo>
                    </div>

                    <Campo
                        label="Ordem na vitrine"
                        inputMode="numeric"
                        value={form.ordem}
                        ajuda="Menor primeiro."
                        onChange={(e) => setCampo("ordem", e.target.value.replace(/\D/g, ""))}
                    />

                    <div className="flex flex-col gap-3">
                        <label className="flex items-center gap-2 font-sans text-sm text-ink">
                            <input
                                type="checkbox"
                                checked={form.habilitado}
                                onChange={(e) => setCampo("habilitado", e.target.checked)}
                                className="h-4 w-4 accent-olive"
                            />
                            Visível na loja
                        </label>
                        <label className="flex items-center gap-2 font-sans text-sm text-ink">
                            <input
                                type="checkbox"
                                checked={form.destaque}
                                onChange={(e) => setCampo("destaque", e.target.checked)}
                                className="h-4 w-4 accent-olive"
                            />
                            Abrir a home como coleção em destaque
                        </label>
                    </div>
                </form>
            </Modal>

            <ConfirmModal
                isOpen={!!confirmar}
                titulo="Excluir a coleção"
                mensagem={`"${confirmar?.nome}" será excluída e as peças perdem esse vínculo de curadoria. As peças em si continuam no catálogo.`}
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
