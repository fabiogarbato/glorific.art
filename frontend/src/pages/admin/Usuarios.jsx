import { useEffect, useMemo, useState } from "react";
import { FiSearch, FiShield, FiUserCheck, FiUserX } from "react-icons/fi";

import {
    CabecalhoPagina,
    EstadoErro,
    EstadoVazio,
} from "@/components/admin/EstadoConsulta.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Modal from "@/components/ui/Modal.jsx";
import Paginacao from "@/components/ui/Paginacao.jsx";
import Tabela from "@/components/ui/Tabela.jsx";

import { useAcoesUsuario, useUsuariosAdmin } from "@/hooks/useUsuariosAdmin.js";
import { useAuth } from "@/hooks/useAuth.js";
import { ITENS_POR_PAGINA } from "@/lib/constants.js";
import { PAPEIS_ATRIBUIVEIS, rotularPapel } from "@/lib/permissoes.js";
import { formatarRelativo } from "@/utils/datas.js";
import { formatCPF, formatTelefone } from "@/utils/masks.js";

/**
 * Usuários e papéis (policy SomenteAdmin).
 *
 * Conceder papel é a operação mais perigosa do sistema — quem consegue conceder
 * "admin" consegue tudo o mais. Por isso ela tem endpoint próprio no backend,
 * fora do formulário de cadastro, e aqui também: o cadastro salva num modal e o
 * papel se concede noutro.
 *
 * A tela desabilita mexer na própria conta, mas a trava de verdade é do
 * servidor: ninguém altera os próprios papéis nem desativa a si mesmo.
 * Revogar um papel derruba as sessões da pessoa, para o privilégio não
 * sobreviver dentro de um token antigo.
 */
export default function Usuarios() {
    const { usuario: eu } = useAuth();

    const [buscaDigitada, setBuscaDigitada] = useState("");
    const [busca, setBusca] = useState("");
    const [papel, setPapel] = useState("");
    const [situacao, setSituacao] = useState("");
    const [pagina, setPagina] = useState(1);

    const [papeisDe, setPapeisDe] = useState(null);
    const [editando, setEditando] = useState(null);
    const [confirmarSituacao, setConfirmarSituacao] = useState(null);

    useEffect(() => {
        const t = setTimeout(() => setBusca(buscaDigitada.trim()), 400);
        return () => clearTimeout(t);
    }, [buscaDigitada]);

    useEffect(() => {
        setPagina(1);
    }, [busca, papel, situacao]);

    const filtros = useMemo(
        () => ({
            search: busca || undefined,
            papel: papel || undefined,
            ativo: situacao === "" ? undefined : situacao === "ativos",
            page: pagina,
            pageSize: ITENS_POR_PAGINA,
        }),
        [busca, papel, situacao, pagina],
    );

    const {
        usuarios,
        total,
        totalPaginas,
        tamanhoPagina,
        isLoading,
        isError,
        refetch,
    } = useUsuariosAdmin(filtros);

    const { atualizar, concederPapel, revogarPapel, ativar, desativar } = useAcoesUsuario();

    const souEu = (u) => !!eu?.uuid && u.uuid === eu.uuid;

    const alternarPapel = (u, nome, tinha) => {
        const acao = tinha ? revogarPapel : concederPapel;
        acao.mutate(
            { id: u.id, papel: nome },
            {
                onSuccess: (atualizado) => {
                    // O modal fica aberto: conceder dois papéis seguidos é o
                    // caso comum, e fechar a cada clique obrigaria a reabrir.
                    if (atualizado) setPapeisDe(atualizado);
                },
            },
        );
    };

    const colunas = [
        {
            chave: "pessoaNome",
            titulo: "Pessoa",
            render: (u) => (
                <div className="min-w-0">
                    <p className="truncate text-sm text-ink">{u.nomeCompleto || "Sem nome"}</p>
                    <p className="truncate text-xs text-ink-soft">{u.email}</p>
                </div>
            ),
        },
        {
            chave: "pessoaPapeis",
            titulo: "Papéis",
            render: (u) =>
                u.roles?.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                        {u.roles.map((r) => (
                            <Badge
                                key={r}
                                variante={r === "admin" ? "destaque" : "contorno"}
                            >
                                {rotularPapel(r)}
                            </Badge>
                        ))}
                    </div>
                ) : (
                    <span className="text-xs text-taupe">—</span>
                ),
        },
        {
            chave: "pessoaContato",
            titulo: "Contato",
            render: (u) => (
                <div className="preco text-xs text-ink-soft">
                    <p>{u.telefone ? formatTelefone(u.telefone) : "—"}</p>
                    <p>{u.cpf ? formatCPF(u.cpf) : ""}</p>
                </div>
            ),
        },
        {
            chave: "pessoaAcesso",
            titulo: "Último acesso",
            render: (u) => (
                <span className="text-xs text-ink-soft">
                    {u.ultimoLoginEm ? formatarRelativo(u.ultimoLoginEm) : "Nunca entrou"}
                </span>
            ),
        },
        {
            chave: "pessoaSituacao",
            titulo: "Situação",
            render: (u) =>
                u.ativo ? (
                    <Badge variante="sucesso">Ativa</Badge>
                ) : (
                    <Badge variante="esgotado">Desativada</Badge>
                ),
        },
        {
            chave: "pessoaAcoes",
            titulo: "",
            alinhamento: "direita",
            render: (u) => (
                <div className="flex justify-end gap-1">
                    <Botao
                        variante="texto"
                        tamanho="sm"
                        onClick={() =>
                            setEditando({
                                id: u.id,
                                nomeCompleto: u.nomeCompleto ?? "",
                                telefone: u.telefone ? formatTelefone(u.telefone) : "",
                                cpf: u.cpf ? formatCPF(u.cpf) : "",
                                aceitaMarketing: !!u.aceitaMarketing,
                            })
                        }
                    >
                        Editar
                    </Botao>

                    <Botao
                        variante="texto"
                        tamanho="sm"
                        disabled={souEu(u)}
                        onClick={() => setPapeisDe(u)}
                    >
                        <FiShield size={13} aria-hidden="true" />
                        <span className="sr-only">Papéis de {u.email}</span>
                    </Botao>

                    <Botao
                        variante="texto"
                        tamanho="sm"
                        disabled={souEu(u)}
                        className={u.ativo ? "text-danger hover:text-danger" : ""}
                        onClick={() => setConfirmarSituacao(u)}
                    >
                        {u.ativo ? (
                            <>
                                <FiUserX size={13} aria-hidden="true" />
                                <span className="sr-only">Desativar {u.email}</span>
                            </>
                        ) : (
                            <>
                                <FiUserCheck size={13} aria-hidden="true" />
                                <span className="sr-only">Reativar {u.email}</span>
                            </>
                        )}
                    </Botao>
                </div>
            ),
        },
    ];

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Configuração"
                titulo="Usuários"
                descricao="Papel define o que a pessoa enxerga do painel. Revogar um papel encerra as sessões dela na hora, para o acesso não sobreviver num token já emitido."
            />

            <div className="mb-6 flex flex-wrap items-end gap-4 border border-sand bg-linen p-4">
                <Campo
                    label="Buscar"
                    placeholder="Nome, e-mail ou CPF"
                    value={buscaDigitada}
                    onChange={(e) => setBuscaDigitada(e.target.value)}
                    containerClassName="min-w-[16rem] flex-1"
                />

                <Campo
                    label="Papel"
                    como="select"
                    value={papel}
                    onChange={(e) => setPapel(e.target.value)}
                    containerClassName="w-48"
                >
                    <option value="">Todos os papéis</option>
                    {PAPEIS_ATRIBUIVEIS.map((p) => (
                        <option key={p} value={p}>
                            {rotularPapel(p)}
                        </option>
                    ))}
                </Campo>

                <Campo
                    label="Situação"
                    como="select"
                    value={situacao}
                    onChange={(e) => setSituacao(e.target.value)}
                    containerClassName="w-48"
                >
                    <option value="">Ativas e desativadas</option>
                    <option value="ativos">Somente ativas</option>
                    <option value="inativos">Somente desativadas</option>
                </Campo>
            </div>

            {isError ? (
                <EstadoErro mensagem="A lista de pessoas não pôde ser carregada." onTentarDeNovo={refetch} />
            ) : !isLoading && usuarios.length === 0 ? (
                <EstadoVazio
                    Icone={FiSearch}
                    titulo="Ninguém com esses filtros"
                    mensagem="Limpe a busca ou troque o papel para ver a lista inteira."
                />
            ) : (
                <>
                    <Tabela
                        colunas={colunas}
                        dados={usuarios}
                        carregando={isLoading}
                        chaveLinha={(u) => u.id}
                        vazio="Ninguém nesta página."
                    />
                    <Paginacao
                        className="mt-6"
                        paginaAtual={pagina}
                        totalPaginas={totalPaginas}
                        totalItens={total}
                        itensPorPagina={tamanhoPagina}
                        onMudarPagina={setPagina}
                    />
                </>
            )}

            {/* ------------------------------------------------------ papéis */}
            <Modal
                isOpen={!!papeisDe}
                onClose={() => setPapeisDe(null)}
                titulo="Papéis desta pessoa"
                largura="sm"
                rodape={
                    <Botao variante="contorno" onClick={() => setPapeisDe(null)}>
                        Fechar
                    </Botao>
                }
            >
                {papeisDe && (
                    <>
                        <p className="mb-4 text-sm text-ink">{papeisDe.email}</p>

                        <ul className="flex flex-col gap-2">
                            {PAPEIS_ATRIBUIVEIS.map((nome) => {
                                const tinha = papeisDe.roles?.includes(nome);
                                const ocupado =
                                    concederPapel.isPending || revogarPapel.isPending;

                                return (
                                    <li
                                        key={nome}
                                        className="flex items-center justify-between gap-4 border border-sand px-3 py-2"
                                    >
                                        <div className="min-w-0">
                                            <p className="text-sm text-ink">{rotularPapel(nome)}</p>
                                            <p className="text-xs text-ink-soft">
                                                {nome === "admin"
                                                    ? "Acesso total, inclusive a esta tela."
                                                    : nome === "gerente"
                                                      ? "Catálogo, preço, cupom e moderação."
                                                      : nome === "operador"
                                                        ? "Pedidos, expedição, etiqueta e estoque."
                                                        : "Compra na loja, sem acesso ao painel."}
                                            </p>
                                        </div>
                                        <Botao
                                            variante={tinha ? "perigo" : "contorno"}
                                            tamanho="sm"
                                            disabled={ocupado}
                                            onClick={() => alternarPapel(papeisDe, nome, tinha)}
                                        >
                                            {tinha ? "Revogar" : "Conceder"}
                                        </Botao>
                                    </li>
                                );
                            })}
                        </ul>

                        <p className="mt-4 text-xs text-taupe">
                            Revogar encerra as sessões abertas dessa pessoa imediatamente.
                        </p>
                    </>
                )}
            </Modal>

            {/* ----------------------------------------------------- cadastro */}
            <Modal
                isOpen={!!editando}
                onClose={() => setEditando(null)}
                titulo="Editar cadastro"
                largura="md"
                rodape={
                    <>
                        <Botao variante="contorno" onClick={() => setEditando(null)}>
                            Cancelar
                        </Botao>
                        <Botao
                            carregando={atualizar.isPending}
                            onClick={() =>
                                atualizar.mutate(editando, { onSuccess: () => setEditando(null) })
                            }
                        >
                            Salvar
                        </Botao>
                    </>
                }
            >
                {editando && (
                    <div className="grid gap-4 sm:grid-cols-2">
                        <Campo
                            label="Nome completo"
                            maxLength={180}
                            value={editando.nomeCompleto}
                            onChange={(e) =>
                                setEditando({ ...editando, nomeCompleto: e.target.value })
                            }
                        />
                        <Campo
                            label="Telefone"
                            inputMode="numeric"
                            maxLength={15}
                            value={editando.telefone}
                            onChange={(e) =>
                                setEditando({
                                    ...editando,
                                    telefone: formatTelefone(e.target.value),
                                })
                            }
                        />
                        <Campo
                            label="CPF"
                            inputMode="numeric"
                            maxLength={14}
                            value={editando.cpf}
                            onChange={(e) =>
                                setEditando({ ...editando, cpf: formatCPF(e.target.value) })
                            }
                        />
                        <label className="flex items-center gap-3 self-end pb-2 text-sm text-ink">
                            <input
                                type="checkbox"
                                className="h-4 w-4 accent-olive"
                                checked={editando.aceitaMarketing}
                                onChange={(e) =>
                                    setEditando({
                                        ...editando,
                                        aceitaMarketing: e.target.checked,
                                    })
                                }
                            />
                            Aceita receber novidades
                        </label>

                        <p className="text-xs text-taupe sm:col-span-2">
                            E-mail e papel não se mudam por aqui: trocar de e-mail exige nova
                            verificação e papel tem tela própria, por ser auditável.
                        </p>
                    </div>
                )}
            </Modal>

            <ConfirmModal
                isOpen={!!confirmarSituacao}
                titulo={confirmarSituacao?.ativo ? "Desativar conta" : "Reativar conta"}
                variante={confirmarSituacao?.ativo ? "perigo" : "primario"}
                textoConfirmar={confirmarSituacao?.ativo ? "Desativar" : "Reativar"}
                carregando={ativar.isPending || desativar.isPending}
                mensagem={
                    confirmarSituacao?.ativo
                        ? `${confirmarSituacao.email} perde o acesso agora e todas as sessões dela são encerradas. Nada é apagado — a conta continua existindo.`
                        : confirmarSituacao
                          ? `${confirmarSituacao.email} volta a conseguir entrar com os papéis que já tinha.`
                          : ""
                }
                onCancel={() => setConfirmarSituacao(null)}
                onConfirm={() => {
                    const acao = confirmarSituacao.ativo ? desativar : ativar;
                    acao.mutate(confirmarSituacao.id, {
                        onSuccess: () => setConfirmarSituacao(null),
                    });
                }}
            />
        </div>
    );
}
