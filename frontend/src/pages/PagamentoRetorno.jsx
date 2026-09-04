import { useEffect, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { FiAlertTriangle, FiCheck, FiClock, FiRefreshCw } from "react-icons/fi";

import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { useStatusCheckout } from "@/hooks/useCheckout.js";
import { useMeusPedidos } from "@/hooks/usePedidos.js";
import { lerCheckoutLembrado } from "@/services/checkoutService.js";
import { queryKeys } from "@/lib/queryKeys.js";

/**
 * Volta do cliente depois do checkout hospedado da InfinitePay.
 *
 * REGRA CENTRAL DESTA TELA: chegar aqui NÃO é prova de pagamento. O retorno é
 * uma URL GET que qualquer pessoa monta, e o parâmetro `resultado` que o backend
 * anexa é só o desfecho da tentativa de conferência daquele instante. Quem
 * aprova é o servidor, depois de consultar o gateway e comparar o valor.
 *
 * Por isso a tela lê `pago` e `terminal` de
 * `GET /api/v1/checkout/{uuid}/status` e, enquanto o pagamento não fecha, diz
 * "estamos confirmando" — nunca "aprovado".
 *
 * Qual pedido consultar: o uuid guardado na ida (sessionStorage). Se ele se
 * perdeu (outra aba, storage bloqueado), caímos no pedido mais recente da conta.
 */

/** Só para escolher a frase de espera. Nunca decide se está pago. */
const AVISOS_DO_RETORNO = {
    PagamentoNaoEncontrado:
        "Não localizamos essa transação de imediato. Se o valor foi debitado, ele aparece aqui assim que o banco confirmar.",
    DivergenciaDeValor:
        "O valor pago não bateu com o total do pedido. Nossa equipe já foi avisada e vai falar com você.",
    Inconclusivo:
        "O provedor de pagamento demorou a responder. Estamos tentando de novo por aqui.",
};

function Painel({ icone, tom, titulo, children, acoes }) {
    return (
        <div className="shell flex min-h-[60vh] flex-col items-center justify-center py-20 text-center">
            <span
                aria-hidden="true"
                className={`flex h-14 w-14 items-center justify-center border ${tom}`}
            >
                {icone}
            </span>

            <h1 className="mt-8 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                {titulo}
            </h1>

            <div className="mt-5 max-w-lg text-base leading-relaxed text-ink-soft">{children}</div>

            {acoes && <div className="mt-10 flex flex-wrap justify-center gap-3">{acoes}</div>}
        </div>
    );
}

export default function PagamentoRetorno() {
    const [params] = useSearchParams();
    const queryClient = useQueryClient();

    const resultado = params.get("resultado");
    // Aceita o uuid pela URL também: útil para reabrir o acompanhamento de um
    // pedido específico sem depender do storage da sessão.
    const uuidDaUrl = params.get("pedido");

    const lembrado = useMemo(() => uuidDaUrl || lerCheckoutLembrado(), [uuidDaUrl]);

    // Sem uuid guardado, o pedido mais recente da conta é a melhor aposta.
    const { pedidos, isLoading: buscandoUltimo } = useMeusPedidos(1, { habilitado: !lembrado });
    const uuid = lembrado ?? pedidos[0]?.uuid ?? null;

    const { status, pago, terminal, aguardandoDemais, isLoading, refetch } = useStatusCheckout(
        uuid,
        { habilitado: !!uuid },
    );

    // Pagamento confirmado muda a lista de pedidos e zera o carrinho no servidor.
    useEffect(() => {
        if (!pago) return;
        queryClient.invalidateQueries({ queryKey: queryKeys.pedidos.all });
        queryClient.invalidateQueries({ queryKey: queryKeys.carrinho.all });
    }, [pago, queryClient]);

    // ------------------------------------------------------------- carregando
    if (buscandoUltimo || (uuid && isLoading)) {
        return (
            <div className="shell flex min-h-[60vh] flex-col items-center justify-center gap-4 py-20">
                <Skeleton className="h-14 w-14" />
                <Skeleton className="h-8 w-72" />
                <Skeleton className="h-4 w-96 max-w-full" />
            </div>
        );
    }

    // ------------------------------------- nenhum pedido para acompanhar
    if (!uuid || !status) {
        return (
            <Painel
                icone={<FiAlertTriangle size={22} className="text-ink-soft" />}
                tom="border-sand bg-linen"
                titulo="Não encontramos esse pedido"
                acoes={
                    <>
                        <Botao to="/conta/pedidos">Ver meus pedidos</Botao>
                        <Botao to="/" variante="contorno">
                            Voltar ao início
                        </Botao>
                    </>
                }
            >
                <p>
                    Pode ser que o pedido tenha sido aberto em outro dispositivo. Ele está listado
                    na sua conta, com o estado real do pagamento.
                </p>
            </Painel>
        );
    }

    // ----------------------------------------------------------------- pago
    if (pago) {
        return (
            <Painel
                icone={<FiCheck size={24} className="text-olive" />}
                tom="border-olive bg-linen"
                titulo="Pagamento confirmado"
                acoes={
                    <>
                        <Botao to={`/conta/pedidos/${status.uuid}`}>Acompanhar o pedido</Botao>
                        <Botao to="/catalogo" variante="contorno">
                            Continuar comprando
                        </Botao>
                    </>
                }
            >
                <p>
                    Recebemos e conferimos o pagamento do pedido{" "}
                    <strong className="font-sans text-ink">{status.numero}</strong>. Agora a peça
                    entra em separação. Avisamos por e-mail a cada passo.
                </p>
            </Painel>
        );
    }

    // ------------------------------------- terminal e NÃO aprovado
    if (terminal) {
        return (
            <Painel
                icone={<FiAlertTriangle size={22} className="text-danger" />}
                tom="border-danger bg-linen"
                titulo="O pagamento não foi concluído"
                acoes={
                    <>
                        {status.paymentUrl && (
                            <Botao href={status.paymentUrl}>Tentar pagar de novo</Botao>
                        )}
                        <Botao to={`/conta/pedidos/${status.uuid}`} variante="contorno">
                            Ver o pedido
                        </Botao>
                        <Botao to="/carrinho" variante="texto">
                            Voltar à sacola
                        </Botao>
                    </>
                }
            >
                <p>
                    O pedido <strong className="font-sans text-ink">{status.numero}</strong> não
                    teve o pagamento aprovado. Nada foi cobrado de você. Se preferir outro meio de
                    pagamento, é só recomeçar.
                </p>
            </Painel>
        );
    }

    // ------------------------------------------------ pendente: confirmando
    return (
        <Painel
            icone={<FiClock size={22} className="text-ink-soft" />}
            tom="border-sand bg-linen"
            titulo="Estamos confirmando seu pagamento"
            acoes={
                <>
                    <Botao variante="contorno" onClick={() => refetch()}>
                        <FiRefreshCw size={14} aria-hidden="true" />
                        Verificar agora
                    </Botao>
                    <Botao to={`/conta/pedidos/${status.uuid}`} variante="texto">
                        Ver o pedido
                    </Botao>
                </>
            }
        >
            <p aria-live="polite">
                O pedido <strong className="font-sans text-ink">{status.numero}</strong> foi criado
                e aguarda a confirmação do banco. Isso costuma levar alguns segundos; pode deixar
                esta página aberta.
            </p>

            {AVISOS_DO_RETORNO[resultado] && (
                <p className="mt-4 text-sm">{AVISOS_DO_RETORNO[resultado]}</p>
            )}

            {aguardandoDemais && (
                <p className="mt-4 text-sm">
                    A confirmação está demorando mais que o normal. Você pode fechar esta página
                    com tranquilidade: o pedido continua na sua conta e o estado é atualizado assim
                    que o banco responder.
                </p>
            )}
        </Painel>
    );
}
