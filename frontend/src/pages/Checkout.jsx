import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { FiAlertTriangle, FiCheck, FiEdit2, FiLock, FiPlus } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import ResumoPedido from "@/components/compra/ResumoPedido.jsx";
import SeletorFrete from "@/components/compra/SeletorFrete.jsx";
import FormEndereco from "@/components/compra/FormEndereco.jsx";

import { useCarrinho } from "@/hooks/useCarrinho.js";
import { useCotacaoFrete } from "@/hooks/useFrete.js";
import { useFinalizarCheckout } from "@/hooks/useCheckout.js";
import {
    useAtualizarEndereco,
    useCriarEndereco,
    useEnderecos,
} from "@/hooks/useConta.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { formatCEP, isValidCPF } from "@/utils/masks.js";

/**
 * Checkout em passo a passo numa página só:
 * endereço → frete → revisão → pagar.
 *
 * O corpo que sai daqui carrega SOMENTE escolhas (`idEndereco`,
 * `idServicoFrete`, cupom, observação). Preço, desconto, frete e total são
 * recalculados no servidor. Nenhum valor exibido nesta tela é usado para cobrar.
 *
 * No fim o backend devolve a URL da InfinitePay e o cliente SAI do site. A volta
 * dele cai em `/checkout/retorno`, que nunca comemora sozinha: quem aprova o
 * pagamento é o backend, depois de conferir no gateway.
 */

function Passo({ ordem, titulo, ativo = true, concluido = false, acao, children }) {
    return (
        <section
            aria-label={titulo}
            className={`border-t border-sand py-8 transition-opacity ${ativo ? "" : "opacity-45"}`}
        >
            <header className="mb-6 flex flex-wrap items-center justify-between gap-3">
                <h2 className="flex items-center gap-3 font-display text-xl tracking-tight text-ink">
                    <span
                        aria-hidden="true"
                        className={`flex h-7 w-7 shrink-0 items-center justify-center border font-sans text-xs ${
                            concluido
                                ? "border-olive bg-olive text-bone"
                                : "border-sand text-ink-soft"
                        }`}
                    >
                        {concluido ? <FiCheck size={13} /> : ordem}
                    </span>
                    {titulo}
                </h2>
                {acao}
            </header>

            {children}
        </section>
    );
}

export default function Checkout() {
    const navigate = useNavigate();

    const { carrinho, itens, vazio, isLoading: carregandoCarrinho, possuiItemIndisponivel } =
        useCarrinho();

    const { enderecos, principal, isLoading: carregandoEnderecos, isError: erroEnderecos } =
        useEnderecos();

    const criarEndereco = useCriarEndereco();
    const atualizarEndereco = useAtualizarEndereco();
    const finalizar = useFinalizarCheckout();

    const [idEndereco, setIdEndereco] = useState(null);
    const [editando, setEditando] = useState(null); // null | 'novo' | id do endereço
    const [freteEscolhido, setFreteEscolhido] = useState(null);
    const [observacao, setObservacao] = useState("");

    // Pré-seleciona o endereço principal assim que a lista chega.
    useEffect(() => {
        if (idEndereco == null && principal) setIdEndereco(principal.id);
    }, [principal, idEndereco]);

    const enderecoSelecionado = useMemo(
        () => enderecos.find((e) => e.id === idEndereco) ?? null,
        [enderecos, idEndereco],
    );

    /**
     * O backend RECUSA o checkout sem CPF válido no endereço (a transportadora
     * exige documento para emitir a etiqueta). Endereço antigo pode estar sem —
     * melhor pedir agora do que devolver um 400 na hora de pagar.
     */
    const documentoPendente =
        !!enderecoSelecionado && !isValidCPF(enderecoSelecionado.documentoDestinatario);

    const enderecoPronto = !!enderecoSelecionado && !documentoPendente;

    const cotacao = useCotacaoFrete(enderecoSelecionado?.cep, { habilitado: enderecoPronto });

    // Trocar de endereço invalida o frete escolhido: outro CEP, outro preço.
    useEffect(() => {
        setFreteEscolhido(null);
    }, [idEndereco]);

    const podePagar =
        enderecoPronto && !!freteEscolhido && !vazio && !possuiItemIndisponivel && !finalizar.isPending;

    // As duas ações abaixo engolem a rejeição de propósito: o interceptor do
    // axios já transformou o erro em toast, e deixar a promessa estourar aqui
    // viraria "unhandled rejection" no console sem informar nada a mais.
    async function salvarEndereco(dados) {
        try {
            if (editando === "novo") {
                const criado = await criarEndereco.mutateAsync({
                    ...dados,
                    principal: enderecos.length === 0 ? true : dados.principal,
                });
                if (criado?.id) setIdEndereco(criado.id);
            } else {
                await atualizarEndereco.mutateAsync({ id: editando, dados });
                setIdEndereco(editando);
            }
            setEditando(null);
        } catch {
            // Formulário continua aberto, com o que a pessoa digitou.
        }
    }

    async function pagar() {
        try {
            const criado = await finalizar.mutateAsync({
                idEndereco,
                idServicoFrete: freteEscolhido.idServico,
                codigoCupom: carrinho.codigoCupom,
                observacaoCliente: observacao,
            });

            if (criado?.paymentUrl) {
                // Checkout hospedado: o cliente sai do site. `assign` (e não
                // `replace`) para o botão "voltar" do navegador ainda funcionar.
                window.location.assign(criado.paymentUrl);
                return;
            }

            // Sem link de pagamento o pedido existe do mesmo jeito — a tela de
            // acompanhamento mostra o estado real em vez de sumir com o pedido.
            navigate("/checkout/retorno", { replace: true });
        } catch {
            // O pedido não foi criado (estoque, cupom vencido, CPF recusado).
            // A pessoa continua no checkout, com tudo preenchido.
        }
    }

    // ------------------------------------------------------------- carregando
    if (carregandoCarrinho || carregandoEnderecos) {
        return (
            <div className="shell py-12 lg:py-16" aria-busy="true">
                <Skeleton className="h-9 w-56" />
                <div className="mt-10 grid gap-12 lg:grid-cols-[1fr_360px] lg:gap-16">
                    <div className="flex flex-col gap-6">
                        <Skeleton className="h-40 w-full" />
                        <Skeleton className="h-40 w-full" />
                    </div>
                    <Skeleton className="h-72 w-full" />
                </div>
            </div>
        );
    }

    // ---------------------------------------------------- carrinho sem itens
    if (vazio) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <h1 className="font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Não há nada para fechar
                </h1>
                <p className="mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                    Sua sacola está vazia. Escolha as peças e volte aqui para concluir a compra.
                </p>
                <Botao to="/catalogo" className="mt-10">
                    Ver a vitrine
                </Botao>
            </div>
        );
    }

    const freteCentavos = freteEscolhido ? freteEscolhido.valorCentavos : null;

    return (
        <div className="shell py-12 lg:py-16">
            <header className="pb-8">
                <p className="eyebrow">Finalizar compra</p>
                <h1 className="mt-3 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Checkout
                </h1>
            </header>

            {possuiItemIndisponivel && (
                <p
                    role="alert"
                    className="mb-8 flex items-start gap-3 border-l-2 border-danger bg-linen px-4 py-3 text-sm text-ink"
                >
                    <FiAlertTriangle
                        size={16}
                        className="mt-0.5 shrink-0 text-danger"
                        aria-hidden="true"
                    />
                    Uma das peças ficou indisponível.{" "}
                    <Link to="/carrinho" className="underline underline-offset-4">
                        Ajuste a sacola
                    </Link>{" "}
                    para continuar.
                </p>
            )}

            <div className="grid gap-12 lg:grid-cols-[1fr_360px] lg:items-start lg:gap-16">
                <div>
                    {/* ------------------------------------------- 1. endereço */}
                    <Passo
                        ordem={1}
                        titulo="Endereço de entrega"
                        concluido={enderecoPronto && !editando}
                        acao={
                            !editando &&
                            enderecos.length > 0 && (
                                <Botao
                                    variante="texto"
                                    tamanho="sm"
                                    onClick={() => setEditando("novo")}
                                >
                                    <FiPlus size={14} aria-hidden="true" />
                                    Novo endereço
                                </Botao>
                            )
                        }
                    >
                        {erroEnderecos && (
                            <p role="alert" className="text-sm text-danger">
                                Não conseguimos carregar seus endereços. Recarregue a página.
                            </p>
                        )}

                        {editando ? (
                            <FormEndereco
                                valorInicial={
                                    editando === "novo"
                                        ? null
                                        : enderecos.find((e) => e.id === editando)
                                }
                                mostrarPrincipal={enderecos.length > 0}
                                salvando={criarEndereco.isPending || atualizarEndereco.isPending}
                                onSubmit={salvarEndereco}
                                onCancelar={enderecos.length > 0 ? () => setEditando(null) : undefined}
                                textoConfirmar="Usar este endereço"
                            />
                        ) : enderecos.length === 0 ? (
                            <div className="flex flex-col items-start gap-4">
                                <p className="text-base leading-relaxed text-ink-soft">
                                    Você ainda não cadastrou um endereço. Precisamos dele para
                                    calcular o frete e emitir a etiqueta.
                                </p>
                                <Botao onClick={() => setEditando("novo")}>
                                    Cadastrar endereço
                                </Botao>
                            </div>
                        ) : (
                            <>
                                <fieldset className="flex flex-col gap-2">
                                    <legend className="sr-only">Escolha o endereço</legend>

                                    {enderecos.map((endereco) => {
                                        const marcado = endereco.id === idEndereco;

                                        return (
                                            <label
                                                key={endereco.id}
                                                className={`flex cursor-pointer items-start gap-3 border px-4 py-3.5 transition-colors ${
                                                    marcado
                                                        ? "border-olive bg-linen"
                                                        : "border-sand hover:border-taupe"
                                                }`}
                                            >
                                                <input
                                                    type="radio"
                                                    name="endereco-entrega"
                                                    value={endereco.id}
                                                    checked={marcado}
                                                    onChange={() => setIdEndereco(endereco.id)}
                                                    className="mt-1 h-4 w-4 shrink-0 accent-olive"
                                                />

                                                <span className="min-w-0 flex-1">
                                                    <span className="block font-sans text-sm text-ink">
                                                        {endereco.destinatario}
                                                        {endereco.apelido && (
                                                            <span className="text-ink-soft">
                                                                {" "}
                                                                · {endereco.apelido}
                                                            </span>
                                                        )}
                                                    </span>
                                                    <span className="mt-1 block text-sm text-ink-soft">
                                                        {endereco.logradouro}, {endereco.numero}
                                                        {endereco.complemento
                                                            ? `, ${endereco.complemento}`
                                                            : ""}
                                                        <br />
                                                        {endereco.bairro} · {endereco.cidade}/
                                                        {endereco.uf} ·{" "}
                                                        {endereco.cepFormatado ??
                                                            formatCEP(endereco.cep)}
                                                    </span>
                                                </span>

                                                <button
                                                    type="button"
                                                    onClick={() => setEditando(endereco.id)}
                                                    aria-label={`Editar o endereço de ${endereco.destinatario}`}
                                                    className="flex h-9 w-9 shrink-0 items-center justify-center text-ink-soft transition-colors hover:text-ink"
                                                >
                                                    <FiEdit2 size={15} />
                                                </button>
                                            </label>
                                        );
                                    })}
                                </fieldset>

                                {documentoPendente && (
                                    <p
                                        role="alert"
                                        className="mt-4 flex flex-wrap items-center gap-2 border-l-2 border-warning bg-linen px-4 py-3 text-sm text-ink"
                                    >
                                        Falta o CPF de quem recebe neste endereço. A transportadora
                                        exige o documento para emitir a etiqueta.
                                        <Botao
                                            variante="texto"
                                            tamanho="sm"
                                            onClick={() => setEditando(idEndereco)}
                                        >
                                            Completar agora
                                        </Botao>
                                    </p>
                                )}
                            </>
                        )}
                    </Passo>

                    {/* ---------------------------------------------- 2. frete */}
                    <Passo
                        ordem={2}
                        titulo="Forma de entrega"
                        ativo={enderecoPronto}
                        concluido={!!freteEscolhido}
                    >
                        {!enderecoPronto ? (
                            <p className="text-sm text-ink-soft">
                                Escolha o endereço para vermos as opções de entrega.
                            </p>
                        ) : (
                            <SeletorFrete
                                titulo="Opções de entrega"
                                opcoes={cotacao.opcoes}
                                idSelecionado={freteEscolhido?.idServico}
                                onSelecionar={setFreteEscolhido}
                                isLoading={cotacao.isLoading}
                                isError={cotacao.isError}
                                vazio={cotacao.vazio}
                                onTentarNovamente={cotacao.refetch}
                            />
                        )}
                    </Passo>

                    {/* -------------------------------------------- 3. revisão */}
                    <Passo ordem={3} titulo="Revisão" ativo={!!freteEscolhido}>
                        <ul className="flex flex-col gap-4">
                            {itens.map((item) => (
                                <li key={item.id} className="flex items-start justify-between gap-4">
                                    <div className="min-w-0">
                                        <p className="font-sans text-sm text-ink">
                                            {item.quantidade} × {item.nomeProduto}
                                        </p>
                                        <p className="mt-0.5 text-sm text-ink-soft">
                                            {[item.tamanho && `Tamanho ${item.tamanho}`, item.cor]
                                                .filter(Boolean)
                                                .join(" · ") || item.sku}
                                        </p>
                                    </div>
                                    <p className="preco shrink-0 text-sm text-ink">
                                        {formatarCentavosParaBRL(item.totalLinhaCentavos)}
                                    </p>
                                </li>
                            ))}
                        </ul>

                        <Campo
                            id="observacao-cliente"
                            como="textarea"
                            rows={3}
                            label="Observação para a loja"
                            maxLength={500}
                            containerClassName="mt-8"
                            placeholder="Um recado sobre a entrega ou um pedido de embrulho, por exemplo."
                            value={observacao}
                            onChange={(e) => setObservacao(e.target.value)}
                            ajuda={`${observacao.length}/500`}
                        />
                    </Passo>
                </div>

                {/* ----------------------------------------------- 4. pagar */}
                <aside className="lg:sticky lg:top-32">
                    <ResumoPedido
                        subtotalCentavos={carrinho.subtotalCentavos}
                        descontoCentavos={carrinho.descontoCentavos}
                        freteCentavos={freteCentavos}
                        freteGratis={!!freteEscolhido?.gratis || carrinho.freteGratisPorCupom}
                        codigoCupom={carrinho.codigoCupom}
                        quantidadeItens={carrinho.quantidadeItens}
                    >
                        <Botao
                            blocoCompleto
                            onClick={pagar}
                            disabled={!podePagar}
                            carregando={finalizar.isPending}
                        >
                            <FiLock size={14} aria-hidden="true" />
                            Ir para o pagamento
                        </Botao>

                        <p className="text-center text-xs leading-relaxed text-ink-soft">
                            Você será levado ao ambiente seguro da InfinitePay para pagar e volta
                            para cá em seguida. Os valores são conferidos pela loja antes da
                            confirmação.
                        </p>
                    </ResumoPedido>
                </aside>
            </div>
        </div>
    );
}
