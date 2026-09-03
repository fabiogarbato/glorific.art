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
import { Swatch } from "@/components/admin/SeletorCor.jsx";
import { COL } from "@/components/admin/chaves.js";

import { useListaAdmin } from "@/hooks/admin/useListaAdmin.js";
import { useOrdenacaoLocal } from "@/hooks/admin/useOrdenacaoLocal.js";
import { useCoresAdmin, useMutacoesCor } from "@/hooks/admin/useCoresAdmin.js";
import { useMidiasAdmin } from "@/hooks/admin/useMidiasAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { HEX_VALIDO, LIMITES } from "@/lib/dominioCatalogo.js";

/**
 * Cores e swatches.
 *
 * O backend só aceita `#RRGGBB` — o regex do DTO recusa "#fff" e "terracota",
 * porque a loja pinta a bolinha direto com esse valor e um hex inválido some da
 * tela. A validação aqui é a mesma, para o erro aparecer antes do envio.
 *
 * Estampa (xadrez, floral) não tem cor chapada que a represente: para esses
 * casos existe a imagem de swatch, que vence a bolinha na exibição.
 */
const HEX_PADRAO = "#B08D57";

const FORM_VAZIO = {
    nome: "",
    slug: "",
    hexRgb: HEX_PADRAO,
    idMidiaSwatch: "",
    ordem: "0",
    ativo: true,
};

function paraForm(cor) {
    return {
        nome: cor.nome ?? "",
        slug: cor.slug ?? "",
        hexRgb: cor.hexRgb || HEX_PADRAO,
        idMidiaSwatch: cor.idMidiaSwatch ?? "",
        ordem: String(cor.ordem ?? 0),
        ativo: !!cor.ativo,
    };
}

export default function Cores() {
    const toast = useToast();

    const lista = useListaAdmin({ q: "" });
    const { itens, total, totalPaginas, isLoading, isError, refetch } = useCoresAdmin({
        pagina: lista.pagina,
        tamanhoPagina: lista.tamanhoPagina,
    });

    const { itens: midias } = useMidiasAdmin({ pagina: 1, tamanhoPagina: 100 });
    const { criar, atualizar, remover } = useMutacoesCor();

    const [edicao, setEdicao] = useState(null);
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [confirmar, setConfirmar] = useState(null);

    useEffect(() => {
        if (!edicao) return;
        setForm(edicao.cor ? paraForm(edicao.cor) : FORM_VAZIO);
        setErros({});
    }, [edicao]);

    const filtradas = useMemo(() => {
        const termo = lista.filtros.q.trim().toLowerCase();
        if (!termo) return itens;
        return itens.filter(
            (c) =>
                c.nome.toLowerCase().includes(termo) ||
                (c.hexRgb ?? "").toLowerCase().includes(termo),
        );
    }, [itens, lista.filtros.q]);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(filtradas);

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const salvar = async (evento) => {
        evento.preventDefault();

        const encontrados = {};
        if (form.nome.trim().length < 2)
            encontrados.nome = "O nome deve ter entre 2 e 80 caracteres.";
        if (!HEX_VALIDO.test(form.hexRgb))
            encontrados.hexRgb = "Informe a cor no formato #RRGGBB, com os seis dígitos.";

        setErros(encontrados);
        if (Object.keys(encontrados).length > 0) return;

        const payload = {
            nome: form.nome.trim(),
            slug: form.slug.trim() || null,
            hexRgb: form.hexRgb.toUpperCase(),
            idMidiaSwatch: form.idMidiaSwatch === "" ? null : Number(form.idMidiaSwatch),
            ordem: Number(form.ordem) || 0,
            ativo: form.ativo,
        };

        try {
            if (edicao.cor) {
                await atualizar.mutateAsync({ id: edicao.cor.id, payload });
                toast.success("Cor salva.");
            } else {
                await criar.mutateAsync(payload);
                toast.success("Cor criada.");
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
            titulo: "Cor",
            ordenavel: true,
            render: (c) => (
                <div className="flex min-w-0 items-center gap-3">
                    <Swatch cor={c} tamanho={22} />
                    <div className="min-w-0">
                        <p className="truncate font-sans text-sm text-ink">{c.nome}</p>
                        <span className="text-xs text-taupe">{c.slug}</span>
                    </div>
                </div>
            ),
        },
        {
            chave: COL.hexRgb,
            titulo: "Hexadecimal",
            render: (c) => <span className="preco uppercase">{c.hexRgb}</span>,
        },
        {
            chave: COL.ordem,
            titulo: "Ordem",
            ordenavel: true,
            alinhamento: "direita",
            render: (c) => <span className="preco">{c.ordem}</span>,
        },
        {
            chave: COL.ativo,
            titulo: "Situação",
            ordenavel: true,
            render: (c) => (
                <Badge variante={c.ativo ? "neutro" : "esgotado"}>
                    {c.ativo ? "Em uso" : "Fora de uso"}
                </Badge>
            ),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (c) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" onClick={() => setEdicao({ cor: c })}>
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
                titulo="Cores"
                descricao="Cada cor vira um swatch na página da peça. Estampa usa imagem, porque cor chapada não representa xadrez nem floral."
                acoes={
                    <Botao onClick={() => setEdicao({ cor: null })}>
                        <FiPlus size={14} aria-hidden="true" />
                        Nova cor
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar as cores"
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
                        rotulo="Filtrar cor"
                        placeholder="Nome ou hexadecimal"
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
                                total === 0 ? "Nenhuma cor cadastrada" : "Nenhuma cor com esse filtro"
                            }
                            mensagem={
                                total === 0
                                    ? "Sem cores não há matriz de variações. Cadastre as cores da estação com o hexadecimal exato do tecido, para o swatch da loja não mentir sobre a peça."
                                    : "Nenhuma cor desta página bate com o texto digitado. Limpe o filtro ou avance para a próxima página."
                            }
                            acao={
                                total === 0 ? (
                                    <Botao onClick={() => setEdicao({ cor: null })}>
                                        <FiPlus size={14} aria-hidden="true" />
                                        Criar cor
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
                                        subtitulo={c.hexRgb}
                                        selo={
                                            <div className="flex items-center gap-2">
                                                <Swatch cor={c} tamanho={20} />
                                                <Badge variante={c.ativo ? "neutro" : "esgotado"}>
                                                    {c.ativo ? "Em uso" : "Fora de uso"}
                                                </Badge>
                                            </div>
                                        }
                                        campos={[{ rotulo: "Ordem", valor: c.ordem }]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    onClick={() => setEdicao({ cor: c })}
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
                titulo={edicao?.cor ? "Editar cor" : "Nova cor"}
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEdicao(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            form="form-cor"
                            type="submit"
                            carregando={criar.isPending || atualizar.isPending}
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                <form id="form-cor" onSubmit={salvar} noValidate className="flex flex-col gap-5">
                    <Campo
                        label="Nome"
                        obrigatorio
                        value={form.nome}
                        erro={erros.nome}
                        maxLength={LIMITES.corNome}
                        placeholder="Terracota"
                        onChange={(e) => setCampo("nome", e.target.value)}
                    />

                    <Campo
                        label="Endereço na loja"
                        value={form.slug}
                        maxLength={LIMITES.corSlug}
                        ajuda={
                            edicao?.cor
                                ? "Em branco, mantém o endereço atual."
                                : "Em branco, o endereço é gerado a partir do nome."
                        }
                        onChange={(e) => setCampo("slug", e.target.value)}
                    />

                    <div className="flex flex-col gap-1.5">
                        <label htmlFor="cor-hex" className="eyebrow">
                            Cor hexadecimal<span className="ml-1 text-danger">*</span>
                        </label>
                        <div className="flex items-center gap-3">
                            <input
                                id="cor-seletor"
                                type="color"
                                aria-label="Escolher a cor visualmente"
                                value={HEX_VALIDO.test(form.hexRgb) ? form.hexRgb : HEX_PADRAO}
                                onChange={(e) => setCampo("hexRgb", e.target.value.toUpperCase())}
                                className="h-11 w-14 shrink-0 cursor-pointer border border-sand bg-base-100 p-1"
                            />
                            <input
                                id="cor-hex"
                                type="text"
                                value={form.hexRgb}
                                maxLength={7}
                                placeholder="#B08D57"
                                aria-invalid={erros.hexRgb ? true : undefined}
                                aria-describedby="cor-hex-ajuda"
                                onChange={(e) => setCampo("hexRgb", e.target.value)}
                                className={`preco w-full border bg-base-100 px-3 py-2.5 font-sans text-base uppercase text-ink transition-colors focus:outline-none ${
                                    erros.hexRgb
                                        ? "border-danger focus:border-danger"
                                        : "border-sand focus:border-olive"
                                }`}
                            />
                        </div>
                        <p
                            id="cor-hex-ajuda"
                            className={`text-xs ${erros.hexRgb ? "text-danger" : "text-ink-soft"}`}
                            role={erros.hexRgb ? "alert" : undefined}
                        >
                            {erros.hexRgb ||
                                "Seis dígitos, como #B08D57. O formato curto de três dígitos é recusado pelo servidor."}
                        </p>
                    </div>

                    <Campo
                        label="Imagem de swatch"
                        como="select"
                        value={form.idMidiaSwatch}
                        ajuda="Para estampa. Quando existe, a imagem substitui a bolinha na loja."
                        onChange={(e) => setCampo("idMidiaSwatch", e.target.value)}
                    >
                        <option value="">Usar a cor chapada</option>
                        {midias.map((midia) => (
                            <option key={midia.id} value={midia.id}>
                                {midia.altText || midia.url}
                            </option>
                        ))}
                    </Campo>

                    <Campo
                        label="Ordem"
                        inputMode="numeric"
                        value={form.ordem}
                        ajuda="Menor primeiro, na ordem em que os swatches aparecem."
                        onChange={(e) => setCampo("ordem", e.target.value.replace(/\D/g, ""))}
                    />

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
                titulo="Excluir a cor"
                mensagem={`"${confirmar?.nome}" será excluída. Cores já usadas em alguma variação são recusadas pelo servidor — nesse caso, desmarque "disponível para novas variações" para tirá-la de circulação sem apagar nada.`}
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
