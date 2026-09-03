import Badge from "@/components/ui/Badge.jsx";

/**
 * Mapa de status espelhando os enums do backend (`Domain/Enums/Enums.cs`).
 * O backend serializa o NOME do valor, nunca o inteiro.
 *
 * A chave crua nunca vai para a tela: status desconhecido (enum novo no
 * servidor, front antigo) cai no rotulo neutro em vez de imprimir
 * "AguardandoPagamento" no rosto do cliente.
 */

/** StatusPedido — o estado que o cliente acompanha. */
export const STATUS_PEDIDO = {
    AguardandoPagamento: { rotulo: "Aguardando pagamento", variante: "alerta" },
    Pago: { rotulo: "Pagamento confirmado", variante: "sucesso" },
    EmSeparacao: { rotulo: "Em separação", variante: "neutro" },
    Enviado: { rotulo: "A caminho", variante: "contorno" },
    Entregue: { rotulo: "Entregue", variante: "sucesso" },
    Cancelado: { rotulo: "Cancelado", variante: "esgotado" },
    PagamentoRecusado: { rotulo: "Pagamento recusado", variante: "erro" },
    EmDevolucao: { rotulo: "Em devolução", variante: "alerta" },
    Devolvido: { rotulo: "Devolvido", variante: "neutro" },
    Estornado: { rotulo: "Estornado", variante: "neutro" },
};

/** StatusPagamento — detalhe da cobrança, exibido no recibo. */
export const STATUS_PAGAMENTO = {
    Pendente: { rotulo: "Pendente", variante: "alerta" },
    Aprovado: { rotulo: "Aprovado", variante: "sucesso" },
    Recusado: { rotulo: "Recusado", variante: "erro" },
    Expirado: { rotulo: "Expirado", variante: "esgotado" },
    Cancelado: { rotulo: "Cancelado", variante: "esgotado" },
    Estornado: { rotulo: "Estornado", variante: "neutro" },
};

/** StatusEnvio — o que a expedição e a transportadora reportam. */
export const STATUS_ENVIO = {
    Pendente: { rotulo: "Aguardando expedição", variante: "neutro" },
    NoCarrinho: { rotulo: "Preparando envio", variante: "neutro" },
    Comprado: { rotulo: "Frete contratado", variante: "neutro" },
    EtiquetaGerada: { rotulo: "Etiqueta emitida", variante: "neutro" },
    Postado: { rotulo: "Postado", variante: "contorno" },
    Entregue: { rotulo: "Entregue", variante: "sucesso" },
    Cancelado: { rotulo: "Cancelado", variante: "esgotado" },
    Falha: { rotulo: "Falha no envio", variante: "erro" },
    AguardandoNota: { rotulo: "Aguardando nota fiscal", variante: "alerta" },
};

const DESCONHECIDO = { rotulo: "Em andamento", variante: "neutro" };

export function descreverStatusPedido(status) {
    return STATUS_PEDIDO[status] ?? DESCONHECIDO;
}

export function descreverStatusPagamento(status) {
    return STATUS_PAGAMENTO[status] ?? DESCONHECIDO;
}

export function descreverStatusEnvio(status) {
    return STATUS_ENVIO[status] ?? DESCONHECIDO;
}

/**
 * @param {{ status?: string, mapa?: 'pedido'|'pagamento'|'envio' }} props
 */
export default function StatusPedidoBadge({ status, mapa = "pedido", className = "" }) {
    const descrever =
        mapa === "pagamento"
            ? descreverStatusPagamento
            : mapa === "envio"
              ? descreverStatusEnvio
              : descreverStatusPedido;

    const { rotulo, variante } = descrever(status);

    return (
        <Badge variante={variante} className={className}>
            {rotulo}
        </Badge>
    );
}
