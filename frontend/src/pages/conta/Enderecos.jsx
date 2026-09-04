import { useState } from "react";
import { FiEdit2, FiPlus, FiStar, FiTrash2 } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Badge from "@/components/ui/Badge.jsx";
import Modal from "@/components/ui/Modal.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { LayoutConta } from "@/components/compra/NavConta.jsx";
import FormEndereco from "@/components/compra/FormEndereco.jsx";
import {
    useCriarEndereco,
    useAtualizarEndereco,
    useDefinirEnderecoPrincipal,
    useEnderecos,
    useRemoverEndereco,
} from "@/hooks/useConta.js";
import { useToast } from "@/hooks/useToast.js";
import { formatCEP, formatTelefone } from "@/utils/masks.js";

/**
 * Endereços de entrega.
 *
 * Só existe um principal por cliente — promover tem endpoint próprio no backend
 * justamente porque o efeito é sobre os OUTROS endereços, e não sobre este.
 */
export default function Enderecos() {
    const { enderecos, isLoading, isError, refetch } = useEnderecos();

    const criar = useCriarEndereco();
    const atualizar = useAtualizarEndereco();
    const remover = useRemoverEndereco();
    const promover = useDefinirEnderecoPrincipal();
    const toast = useToast();

    const [editando, setEditando] = useState(null); // null | 'novo' | endereço
    const [confirmarExclusao, setConfirmarExclusao] = useState(null);

    async function salvar(dados) {
        try {
            if (editando === "novo") {
                await criar.mutateAsync({
                    ...dados,
                    // O primeiro endereço vira principal sozinho: obrigar a marcar
                    // seria pedir uma escolha que não existe.
                    principal: enderecos.length === 0 ? true : dados.principal,
                });
                toast.success("Endereço cadastrado.");
            } else {
                await atualizar.mutateAsync({ id: editando.id, dados });
                toast.success("Endereço atualizado.");
            }
            setEditando(null);
        } catch {
            // Erro já virou toast no interceptor; o formulário continua aberto.
        }
    }

    async function excluir() {
        try {
            await remover.mutateAsync(confirmarExclusao.id);
            toast.success("Endereço removido.");
        } catch {
            /* toast já emitido */
        } finally {
            setConfirmarExclusao(null);
        }
    }

    return (
        <LayoutConta
            titulo="Endereços"
            descricao="Onde suas peças devem chegar. O endereço principal já vem escolhido no checkout."
            acoes={
                enderecos.length > 0 && (
                    <Botao onClick={() => setEditando("novo")}>
                        <FiPlus size={15} aria-hidden="true" />
                        Novo endereço
                    </Botao>
                )
            }
        >
            {isLoading && (
                <div className="flex flex-col gap-4" aria-busy="true">
                    <Skeleton className="h-32 w-full" />
                    <Skeleton className="h-32 w-full" />
                </div>
            )}

            {isError && (
                <div>
                    <p className="text-base text-ink">
                        Não conseguimos carregar seus endereços agora.
                    </p>
                    <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                        Tentar de novo
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && enderecos.length === 0 && (
                <div className="border border-sand bg-linen px-6 py-12 text-center">
                    <h2 className="font-display text-xl tracking-tight text-ink">
                        Nenhum endereço cadastrado
                    </h2>
                    <p className="mx-auto mt-4 max-w-md text-base leading-relaxed text-ink-soft">
                        Cadastre um endereço para calcularmos o frete e emitirmos a etiqueta de
                        envio. Leva menos de um minuto.
                    </p>
                    <Botao className="mt-8" onClick={() => setEditando("novo")}>
                        Cadastrar endereço
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && enderecos.length > 0 && (
                <ul className="flex flex-col gap-4">
                    {enderecos.map((endereco) => (
                        <li
                            key={endereco.id}
                            className={`flex flex-wrap items-start justify-between gap-4 border px-5 py-5 ${
                                endereco.principal ? "border-olive bg-linen" : "border-sand"
                            }`}
                        >
                            <div className="min-w-0">
                                <div className="flex flex-wrap items-center gap-3">
                                    <h2 className="font-sans text-sm text-ink">
                                        {endereco.apelido || endereco.destinatario}
                                    </h2>
                                    {endereco.principal && (
                                        <Badge variante="contorno">Principal</Badge>
                                    )}
                                </div>

                                <p className="mt-2 text-sm leading-relaxed text-ink-soft">
                                    {endereco.destinatario}
                                    <br />
                                    {endereco.logradouro}, {endereco.numero}
                                    {endereco.complemento ? `, ${endereco.complemento}` : ""}
                                    <br />
                                    {endereco.bairro} · {endereco.cidade}/{endereco.uf}
                                    <br />
                                    {endereco.cepFormatado ?? formatCEP(endereco.cep)} ·{" "}
                                    {formatTelefone(endereco.telefoneContato)}
                                </p>
                            </div>

                            <div className="flex items-center gap-1">
                                {!endereco.principal && (
                                    <button
                                        type="button"
                                        onClick={() => promover.mutate(endereco.id)}
                                        disabled={promover.isPending}
                                        aria-label={`Tornar principal o endereço ${endereco.apelido || endereco.destinatario}`}
                                        className="flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-brass disabled:opacity-40"
                                    >
                                        <FiStar size={16} />
                                    </button>
                                )}

                                <button
                                    type="button"
                                    onClick={() => setEditando(endereco)}
                                    aria-label={`Editar o endereço ${endereco.apelido || endereco.destinatario}`}
                                    className="flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-ink"
                                >
                                    <FiEdit2 size={16} />
                                </button>

                                <button
                                    type="button"
                                    onClick={() => setConfirmarExclusao(endereco)}
                                    aria-label={`Remover o endereço ${endereco.apelido || endereco.destinatario}`}
                                    className="flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-danger"
                                >
                                    <FiTrash2 size={16} />
                                </button>
                            </div>
                        </li>
                    ))}
                </ul>
            )}

            <Modal
                isOpen={!!editando}
                onClose={() => setEditando(null)}
                largura="lg"
                titulo={editando === "novo" ? "Novo endereço" : "Editar endereço"}
            >
                {editando && (
                    <FormEndereco
                        valorInicial={editando === "novo" ? null : editando}
                        mostrarPrincipal={editando === "novo" ? enderecos.length > 0 : false}
                        salvando={criar.isPending || atualizar.isPending}
                        onSubmit={salvar}
                        onCancelar={() => setEditando(null)}
                    />
                )}
            </Modal>

            <ConfirmModal
                isOpen={!!confirmarExclusao}
                titulo="Remover endereço"
                mensagem={
                    confirmarExclusao
                        ? `Tem certeza que quer remover o endereço de ${confirmarExclusao.destinatario}? Pedidos já feitos guardam uma cópia própria e não mudam.`
                        : ""
                }
                textoConfirmar="Remover"
                carregando={remover.isPending}
                onConfirm={excluir}
                onCancel={() => setConfirmarExclusao(null)}
            />
        </LayoutConta>
    );
}
