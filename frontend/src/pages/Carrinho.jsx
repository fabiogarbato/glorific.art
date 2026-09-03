import { useState } from "react";
import { Link } from "react-router-dom";
import { FiAlertTriangle, FiArrowRight } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import ItemCarrinho from "@/components/compra/ItemCarrinho.jsx";
import ResumoPedido from "@/components/compra/ResumoPedido.jsx";
import CampoCupom from "@/components/compra/CampoCupom.jsx";
import SeletorFrete from "@/components/compra/SeletorFrete.jsx";
import { useCarrinho } from "@/hooks/useCarrinho.js";
import { useCotacaoFrete } from "@/hooks/useFrete.js";
import { CEP_MAXLENGTH, formatCEP, isValidCEP, onlyDigits } from "@/utils/masks.js";

/**
 * Carrinho.
 *
 * O carrinho mora no servidor: esta tela só desenha o que o backend devolveu e
 * manda de volta a intenção da pessoa. Nenhum preço é recalculado aqui.
 *
 * O frete é um SIMULADOR: cotar não reserva nada. A cotação que vale é a do
 * checkout, refeita no servidor no momento de fechar o pedido.
 */
export default function Carrinho() {
    const {
        carrinho,
        itens,
        vazio,
        isLoading,
        isError,
        recarregar,
        salvando,
        alterarQuantidade,
        remover,
        aplicarCupom,
        removerCupom,
        possuiItemIndisponivel,
    } = useCarrinho();

    const [cep, setCep] = useState("");
    const [cepConsultado, setCepConsultado] = useState("");
    const [freteEscolhido, setFreteEscolhido] = useState(null);

    const cotacao = useCotacaoFrete(cepConsultado, { habilitado: !!cepConsultado });

    /**
     * As mutações do carrinho rejeitam quando a API recusa. Quem clica num botão
     * de quantidade não tem `try/catch`, e o erro já virou toast no interceptor —
     * então aqui a rejeição é absorvida para não virar "unhandled rejection".
     */
    const semEstourar =
        (acao) =>
        (...args) =>
            Promise.resolve(acao(...args)).catch(() => {});

    function calcularFrete(evento) {
        evento.preventDefault();
        if (!isValidCEP(cep)) return;
        setFreteEscolhido(null);
        setCepConsultado(onlyDigits(cep));
    }

    // ------------------------------------------------------------- carregando
    if (isLoading) {
        return (
            <div className="shell py-12 lg:py-16" aria-busy="true">
                <Skeleton className="h-9 w-52" />
                <div className="mt-10 grid gap-12 lg:grid-cols-[1fr_360px] lg:gap-16">
                    <div className="flex flex-col gap-6">
                        {[0, 1, 2].map((i) => (
                            <div key={i} className="flex gap-6 border-b border-sand pb-6">
                                <Skeleton className="aspect-product w-24 sm:w-28" />
                                <div className="flex flex-1 flex-col gap-3">
                                    <Skeleton className="h-5 w-2/3" />
                                    <Skeleton className="h-4 w-1/3" />
                                    <Skeleton className="mt-auto h-10 w-32" />
                                </div>
                            </div>
                        ))}
                    </div>
                    <Skeleton className="h-72 w-full" />
                </div>
            </div>
        );
    }

    // ------------------------------------------------------------------ erro
    if (isError) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <h1 className="font-display text-2xl tracking-tight text-ink">
                    Não conseguimos abrir seu carrinho
                </h1>
                <p className="mt-4 max-w-md text-base leading-relaxed text-ink-soft">
                    A conexão com a loja falhou. Suas peças continuam guardadas — tente de novo em
                    instantes.
                </p>
                <Botao className="mt-10" onClick={() => recarregar()}>
                    Tentar de novo
                </Botao>
            </div>
        );
    }

    // ----------------------------------------------------------------- vazio
    if (vazio) {
        return (
            <div className="shell flex min-h-[50vh] flex-col items-center justify-center py-20 text-center">
                <p className="eyebrow">Sacola</p>
                <h1 className="mt-6 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                    Sua sacola está vazia
                </h1>
                <p className="mt-5 max-w-md text-base leading-relaxed text-ink-soft">
                    Nada escolhido ainda. Comece pela vitrine: as peças de linho e algodão da
                    coleção de estreia estão todas lá.
                </p>
                <div className="mt-10 flex flex-wrap justify-center gap-3">
                    <Botao to="/catalogo">Ver a vitrine</Botao>
                    <Botao to="/colecoes" variante="contorno">
                        Explorar coleções
                    </Botao>
                </div>
            </div>
        );
    }

    // ----------------------------------------------------------------- lista
    const freteCentavos = freteEscolhido ? freteEscolhido.valorCentavos : null;
    const bloqueado = possuiItemIndisponivel || salvando;

    return (
        <div className="shell py-12 lg:py-16">
            <header className="flex flex-wrap items-end justify-between gap-4 pb-8">
                <div>
                    <p className="eyebrow">Sacola</p>
                    <h1 className="mt-3 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                        Seu carrinho
                    </h1>
                </div>
                <p className="text-sm text-ink-soft">
                    {carrinho.quantidadeItens}{" "}
                    {carrinho.quantidadeItens === 1 ? "peça" : "peças"}
                </p>
            </header>

            {possuiItemIndisponivel && (
                <p
                    role="alert"
                    className="mb-8 flex items-start gap-3 border-l-2 border-danger bg-linen px-4 py-3 text-sm text-ink"
                >
                    <FiAlertTriangle size={16} className="mt-0.5 shrink-0 text-danger" aria-hidden="true" />
                    Alguma peça da sacola saiu de estoque. Remova o item marcado para seguir para o
                    pagamento.
                </p>
            )}

            <div className="grid gap-12 lg:grid-cols-[1fr_360px] lg:items-start lg:gap-16">
                <ul className="border-t border-sand">
                    {itens.map((item) => (
                        <ItemCarrinho
                            key={item.id}
                            item={item}
                            salvando={salvando}
                            onAlterarQuantidade={semEstourar(alterarQuantidade)}
                            onRemover={semEstourar(remover)}
                        />
                    ))}
                </ul>

                <aside className="flex flex-col gap-8 lg:sticky lg:top-32">
                    <CampoCupom
                        codigoAplicado={carrinho.codigoCupom}
                        aviso={carrinho.avisoCupom}
                        onAplicar={aplicarCupom}
                        onRemover={semEstourar(removerCupom)}
                        salvando={salvando}
                    />

                    {/* ------------------------------------------ frete */}
                    <div className="flex flex-col gap-4">
                        <form onSubmit={calcularFrete} className="flex flex-col gap-1.5">
                            <label htmlFor="cep-frete" className="eyebrow">
                                Calcular frete
                            </label>
                            <div className="flex gap-2">
                                <input
                                    id="cep-frete"
                                    inputMode="numeric"
                                    autoComplete="postal-code"
                                    maxLength={CEP_MAXLENGTH}
                                    placeholder="00000-000"
                                    value={cep}
                                    onChange={(e) => setCep(formatCEP(e.target.value))}
                                    className="h-11 w-full border border-sand bg-base-100 px-3 font-sans text-sm text-ink placeholder:text-taupe focus:border-olive focus:outline-none"
                                />
                                <Botao
                                    type="submit"
                                    variante="contorno"
                                    disabled={!isValidCEP(cep)}
                                    carregando={cotacao.isLoading}
                                >
                                    Calcular
                                </Botao>
                            </div>
                            <p className="text-xs text-ink-soft">
                                Simulação. O valor final é recalculado no checkout.
                            </p>
                        </form>

                        {cepConsultado && (
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
                    </div>

                    <ResumoPedido
                        subtotalCentavos={carrinho.subtotalCentavos}
                        descontoCentavos={carrinho.descontoCentavos}
                        freteCentavos={freteCentavos}
                        freteGratis={!!freteEscolhido?.gratis || carrinho.freteGratisPorCupom}
                        codigoCupom={carrinho.codigoCupom}
                        quantidadeItens={carrinho.quantidadeItens}
                    >
                        {/*
                          * `to` só entra quando o botão está liberado: `disabled`
                          * num <Link> não impede o clique — viraria um atalho
                          * para o checkout com a sacola travada.
                          */}
                        <Botao
                            to={bloqueado ? undefined : "/checkout"}
                            blocoCompleto
                            disabled={bloqueado}
                        >
                            Fechar pedido
                            <FiArrowRight size={15} aria-hidden="true" />
                        </Botao>

                        <Link
                            to="/catalogo"
                            className="text-center font-sans text-xs uppercase tracking-widest text-ink-soft underline decoration-sand underline-offset-4 transition-colors hover:text-ink"
                        >
                            Continuar comprando
                        </Link>
                    </ResumoPedido>
                </aside>
            </div>
        </div>
    );
}
