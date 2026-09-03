import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { FiAlertTriangle, FiEdit2, FiLayers, FiPackage, FiPlus, FiTag } from "react-icons/fi";

import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import Aviso from "@/components/admin/Aviso.jsx";
import CabecalhoPagina from "@/components/admin/CabecalhoPagina.jsx";
import EstadoVazio from "@/components/admin/EstadoVazio.jsx";
import FaixaKpis from "@/components/admin/FaixaKpis.jsx";
import FiltroBusca from "@/components/admin/FiltroBusca.jsx";
import LinhaTabela from "@/components/admin/LinhaTabela.jsx";
import { COL } from "@/components/admin/chaves.js";

import { useListaAdmin } from "@/hooks/admin/useListaAdmin.js";
import { useOrdenacaoLocal } from "@/hooks/admin/useOrdenacaoLocal.js";
import { useArvoreCategorias } from "@/hooks/admin/useCategoriasAdmin.js";
import { useMutacoesProduto, useProdutosAdmin } from "@/hooks/admin/useProdutosAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Listagem de peças do painel.
 *
 * Paginação e filtros são SERVER-SIDE: `GET /admin/produtos` aceita `q`,
 * `categoria`, `ativo`, `page` e `pageSize`. Nunca carregamos o catálogo
 * inteiro para filtrar no navegador.
 *
 * Uma limitação real da API aparece na tela: o controller faz `ativo ?? true`,
 * então não existe "ver ativas e inativas juntas" — o filtro de situação é
 * binário de propósito, e não uma opção que fingiríamos ter.
 */
export default function ListaProdutos() {
    const navegar = useNavigate();
    const toast = useToast();

    const lista = useListaAdmin({ ativo: true, categoria: "", q: "" });
    const { opcoes: categorias, isLoading: carregandoCategorias } = useArvoreCategorias(false);

    const filtrosApi = useMemo(
        () => ({
            ativo: lista.filtros.ativo,
            categoria: lista.filtros.categoria === "" ? null : Number(lista.filtros.categoria),
            q: lista.filtros.q,
            pagina: lista.pagina,
            tamanhoPagina: lista.tamanhoPagina,
        }),
        [lista.filtros, lista.pagina, lista.tamanhoPagina],
    );

    const { produtos, total, totalPaginas, isLoading, isError, refetch } =
        useProdutosAdmin(filtrosApi);

    const { ordenacao, ordenar, dados } = useOrdenacaoLocal(produtos);
    const { desativar, ativar } = useMutacoesProduto();

    /** Estado "objeto ou null": guarda a peça e a intenção da confirmação. */
    const [confirmar, setConfirmar] = useState(null);

    const kpis = useMemo(() => {
        const skus = produtos.reduce((soma, p) => soma + (p.totalVariacoes ?? 0), 0);
        const disponivel = produtos.reduce(
            (soma, p) => soma + (p.estoqueTotalDisponivel ?? 0),
            0,
        );
        const zeradas = produtos.filter((p) => (p.estoqueTotalDisponivel ?? 0) === 0).length;

        return [
            {
                rotulo: "Peças no filtro",
                valor: total,
                Icone: FiTag,
                ajuda: "Contagem no banco, não só desta página.",
            },
            {
                rotulo: "SKUs nesta página",
                valor: skus,
                Icone: FiLayers,
            },
            {
                rotulo: "Disponível nesta página",
                valor: disponivel,
                Icone: FiPackage,
            },
            {
                rotulo: "Sem estoque",
                valor: zeradas,
                Icone: FiAlertTriangle,
                alerta: zeradas > 0,
                ajuda: "Peças desta página sem saldo disponível.",
            },
        ];
    }, [produtos, total]);

    const confirmarAcao = async () => {
        if (!confirmar) return;
        const { produto, acao } = confirmar;

        try {
            if (acao === "desativar") {
                await desativar.mutateAsync(produto.id);
                toast.success(`"${produto.nome}" saiu do ar.`);
            } else {
                await ativar.mutateAsync(produto.id);
                toast.success(`"${produto.nome}" voltou para a vitrine.`);
            }
            setConfirmar(null);
        } catch {
            // O interceptor do axios já emitiu o toast de erro; o modal fica aberto
            // para o operador tentar de novo sem refazer o caminho.
        }
    };

    const colunas = [
        {
            chave: COL.nome,
            titulo: "Peça",
            ordenavel: true,
            render: (p) => (
                <div className="min-w-0">
                    <Link
                        to={`/admin/produtos/${p.id}`}
                        className="block max-w-[22rem] truncate font-sans text-sm text-ink underline-offset-4 hover:underline"
                    >
                        {p.nome}
                    </Link>
                    <span className="preco text-xs text-taupe">{p.skuBase}</span>
                </div>
            ),
        },
        {
            chave: COL.nomeCategoria,
            titulo: "Categoria",
            ordenavel: true,
            render: (p) => p.nomeCategoria || "—",
        },
        {
            chave: COL.preco,
            titulo: "Preço",
            ordenavel: true,
            alinhamento: "direita",
            render: (p) => (
                <span className="preco">{formatarCentavosParaBRL(p.precoBaseCentavos)}</span>
            ),
        },
        {
            chave: COL.totalVariacoes,
            titulo: "SKUs",
            ordenavel: true,
            alinhamento: "direita",
            render: (p) => <span className="preco">{p.totalVariacoes ?? 0}</span>,
        },
        {
            chave: COL.estoque,
            titulo: "Disponível",
            ordenavel: true,
            alinhamento: "direita",
            render: (p) => (
                <span
                    className={`preco ${(p.estoqueTotalDisponivel ?? 0) === 0 ? "text-danger" : ""}`}
                >
                    {p.estoqueTotalDisponivel ?? 0}
                </span>
            ),
        },
        {
            chave: COL.ativo,
            titulo: "Situação",
            ordenavel: true,
            render: (p) => (
                <Badge variante={p.ativo ? "neutro" : "esgotado"}>
                    {p.ativo ? "Publicada" : "Fora do ar"}
                </Badge>
            ),
        },
        {
            chave: COL.acoes,
            titulo: "Ações",
            alinhamento: "direita",
            render: (p) => (
                <div className="flex justify-end gap-2">
                    <Botao tamanho="sm" variante="sutil" to={`/admin/produtos/${p.id}`}>
                        <FiEdit2 size={13} aria-hidden="true" />
                        Editar
                    </Botao>
                    {p.ativo ? (
                        <Botao
                            tamanho="sm"
                            variante="texto"
                            onClick={() => setConfirmar({ produto: p, acao: "desativar" })}
                        >
                            Tirar do ar
                        </Botao>
                    ) : (
                        <Botao
                            tamanho="sm"
                            variante="texto"
                            onClick={() => setConfirmar({ produto: p, acao: "ativar" })}
                        >
                            Publicar
                        </Botao>
                    )}
                </div>
            ),
        },
    ];

    const semFiltroAplicado = !lista.filtros.q && !lista.filtros.categoria;

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobrancelha="Catálogo"
                titulo="Peças"
                descricao="Cada peça reúne os dados de vitrine, a grade de tamanhos e cores e a galeria de fotos."
                acoes={
                    <Botao to="/admin/produtos/novo">
                        <FiPlus size={14} aria-hidden="true" />
                        Nova peça
                    </Botao>
                }
            />

            {isError ? (
                <Aviso
                    variante="erro"
                    titulo="Não foi possível carregar as peças"
                    acoes={
                        <Botao variante="contorno" tamanho="sm" onClick={() => refetch()}>
                            Tentar de novo
                        </Botao>
                    }
                >
                    <p>
                        A lista não chegou do servidor. Verifique a conexão e tente novamente em
                        alguns instantes.
                    </p>
                </Aviso>
            ) : (
                <>
                    <FaixaKpis itens={kpis} carregando={isLoading} />

                    <FiltroBusca
                        valor={lista.filtros.q}
                        onBuscar={(texto) => lista.definirFiltro("q", texto)}
                        rotulo="Buscar peça"
                        placeholder="Nome, SKU base ou slug"
                        escopo="servidor"
                        tamanhoPagina={lista.tamanhoPagina}
                        onTamanhoPagina={lista.definirTamanhoPagina}
                        onLimpar={lista.limpar}
                    >
                        <div className="flex w-full flex-col gap-1.5 lg:w-64">
                            <label htmlFor="filtro-categoria" className="eyebrow">
                                Categoria
                            </label>
                            <select
                                id="filtro-categoria"
                                value={lista.filtros.categoria}
                                disabled={carregandoCategorias}
                                onChange={(e) => lista.definirFiltro("categoria", e.target.value)}
                                className="w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-base text-ink transition-colors focus:border-olive focus:outline-none"
                            >
                                <option value="">Todas as categorias</option>
                                {categorias.map((categoria) => (
                                    <option key={categoria.id} value={categoria.id}>
                                        {"— ".repeat(categoria.profundidade)}
                                        {categoria.nome}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="flex w-full flex-col gap-1.5 lg:w-48">
                            <label htmlFor="filtro-situacao" className="eyebrow">
                                Situação
                            </label>
                            <select
                                id="filtro-situacao"
                                value={String(lista.filtros.ativo)}
                                onChange={(e) =>
                                    lista.definirFiltro("ativo", e.target.value === "true")
                                }
                                className="w-full border border-sand bg-base-100 px-3 py-2.5 font-sans text-base text-ink transition-colors focus:border-olive focus:outline-none"
                            >
                                <option value="true">Publicadas</option>
                                <option value="false">Fora do ar</option>
                            </select>
                        </div>
                    </FiltroBusca>

                    {isLoading && (
                        <div className="flex flex-col gap-3">
                            {Array.from({ length: 6 }).map((_, i) => (
                                <Skeleton key={`sk-${i}`} className="h-16 w-full" />
                            ))}
                        </div>
                    )}

                    {!isLoading && produtos.length === 0 && (
                        <EstadoVazio
                            titulo={
                                semFiltroAplicado
                                    ? "Nenhuma peça cadastrada ainda"
                                    : "Nenhuma peça com esses filtros"
                            }
                            mensagem={
                                semFiltroAplicado
                                    ? "Cadastre a primeira peça: nome, categoria e preço bastam para começar. A grade de tamanhos e cores e as fotos entram logo depois, na mesma tela."
                                    : "Nenhuma peça publicada bate com a busca e a categoria escolhidas. Ajuste o filtro, troque a situação para ver o que está fora do ar ou limpe tudo."
                            }
                            acao={
                                semFiltroAplicado ? (
                                    <Botao to="/admin/produtos/novo">
                                        <FiPlus size={14} aria-hidden="true" />
                                        Cadastrar peça
                                    </Botao>
                                ) : (
                                    <Botao variante="contorno" onClick={lista.limpar}>
                                        Limpar filtros
                                    </Botao>
                                )
                            }
                        />
                    )}

                    {!isLoading && produtos.length > 0 && (
                        <>
                            {/* Mobile: card por peça */}
                            <div className="flex flex-col gap-3 sm:hidden">
                                {dados.map((p) => (
                                    <LinhaTabela
                                        key={p.id}
                                        titulo={p.nome}
                                        subtitulo={p.skuBase}
                                        onClick={() => navegar(`/admin/produtos/${p.id}`)}
                                        selo={
                                            <Badge variante={p.ativo ? "neutro" : "esgotado"}>
                                                {p.ativo ? "Publicada" : "Fora do ar"}
                                            </Badge>
                                        }
                                        campos={[
                                            { rotulo: "Categoria", valor: p.nomeCategoria || "—" },
                                            {
                                                rotulo: "Preço",
                                                valor: formatarCentavosParaBRL(p.precoBaseCentavos),
                                            },
                                            { rotulo: "SKUs", valor: p.totalVariacoes ?? 0 },
                                            {
                                                rotulo: "Disponível",
                                                valor: p.estoqueTotalDisponivel ?? 0,
                                            },
                                        ]}
                                        acoes={
                                            <>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="sutil"
                                                    to={`/admin/produtos/${p.id}`}
                                                >
                                                    Editar
                                                </Botao>
                                                <Botao
                                                    tamanho="sm"
                                                    variante="texto"
                                                    onClick={() =>
                                                        setConfirmar({
                                                            produto: p,
                                                            acao: p.ativo ? "desativar" : "ativar",
                                                        })
                                                    }
                                                >
                                                    {p.ativo ? "Tirar do ar" : "Publicar"}
                                                </Botao>
                                            </>
                                        }
                                    />
                                ))}
                            </div>

                            {/* Desktop: tabela */}
                            <div className="hidden sm:block">
                                <Tabela
                                    colunas={colunas}
                                    dados={dados}
                                    ordenacao={ordenacao}
                                    onOrdenar={ordenar}
                                />
                            </div>

                            {ordenacao && (
                                <p className="mt-2 text-xs text-taupe">
                                    A ordenação vale para os itens desta página. A API entrega
                                    sempre as peças mais recentes primeiro.
                                </p>
                            )}

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

            <ConfirmModal
                isOpen={!!confirmar}
                titulo={
                    confirmar?.acao === "ativar" ? "Publicar a peça" : "Tirar a peça do ar"
                }
                mensagem={
                    confirmar?.acao === "ativar"
                        ? `"${confirmar?.produto?.nome}" volta a aparecer na vitrine e pode ser comprada.`
                        : `"${confirmar?.produto?.nome}" sai da vitrine. A peça continua existindo, porque o histórico de pedidos aponta para ela, e pode ser publicada de novo quando você quiser.`
                }
                textoConfirmar={confirmar?.acao === "ativar" ? "Publicar" : "Tirar do ar"}
                variante={confirmar?.acao === "ativar" ? "primario" : "perigo"}
                carregando={desativar.isPending || ativar.isPending}
                onConfirm={confirmarAcao}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
