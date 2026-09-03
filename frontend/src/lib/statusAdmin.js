/**
 * Dicionarios de dominio do painel: valor CRU que trafega na API x rotulo que o
 * usuario le x variante de Badge.
 *
 * Mora num `.js` (e nao dentro das pages) por dois motivos:
 *  1. o valor cru precisa ficar SEM acento — e o nome do enum/rotulo do backend;
 *  2. o rotulo precisa ficar COM acento — e copy.
 * Manter os dois no mesmo lugar impede que alguem "corrija" o valor cru e quebre
 * a chamada silenciosamente.
 *
 * Enums do backend viajam como NUMERO (nao ha JsonStringEnumConverter no
 * Program.cs). Onde o DTO ja declara `string` (status de pedido, envio e
 * pagamento) o valor e o NOME do enum. Cada mapa abaixo diz qual e o caso.
 */

// ---------------------------------------------------------------------------
// Pedido — PedidoResponseDto.status e STRING (nome do enum StatusPedido).
// ---------------------------------------------------------------------------
export const STATUS_PEDIDO = [
    { valor: "AguardandoPagamento", rotulo: "Aguardando pagamento", variante: "neutro" },
    { valor: "Pago", rotulo: "Pago", variante: "sucesso" },
    { valor: "EmSeparacao", rotulo: "Em separação", variante: "alerta" },
    { valor: "Enviado", rotulo: "Enviado", variante: "contorno" },
    { valor: "Entregue", rotulo: "Entregue", variante: "sucesso" },
    { valor: "Cancelado", rotulo: "Cancelado", variante: "erro" },
    { valor: "PagamentoRecusado", rotulo: "Pagamento recusado", variante: "erro" },
    { valor: "EmDevolucao", rotulo: "Em devolução", variante: "alerta" },
    { valor: "Devolvido", rotulo: "Devolvido", variante: "esgotado" },
    { valor: "Estornado", rotulo: "Estornado", variante: "esgotado" },
];

/**
 * Status terminais: PedidoService recusa qualquer mudanca a partir deles
 * ("Este pedido esta encerrado e nao aceita mudanca de status").
 */
export const STATUS_PEDIDO_ENCERRADO = ["Cancelado", "Devolvido", "Estornado"];

/**
 * O painel NAO oferece "Cancelado" no seletor de status: o backend rejeita de
 * proposito e manda usar POST /cancelar, que devolve estoque e cancela etiqueta.
 */
export function statusPedidoSelecionaveis(statusAtual) {
    if (STATUS_PEDIDO_ENCERRADO.includes(statusAtual)) return [];
    return STATUS_PEDIDO.filter(
        (s) => s.valor !== "Cancelado" && s.valor !== statusAtual,
    );
}

// ---------------------------------------------------------------------------
// Envio — string no detalhe do pedido, numero + `statusNome` no dashboard.
// ---------------------------------------------------------------------------
export const STATUS_ENVIO = [
    { valor: "Pendente", rotulo: "Pendente", variante: "neutro" },
    { valor: "NoCarrinho", rotulo: "No carrinho da transportadora", variante: "neutro" },
    { valor: "Comprado", rotulo: "Frete comprado", variante: "contorno" },
    { valor: "EtiquetaGerada", rotulo: "Etiqueta gerada", variante: "contorno" },
    { valor: "Postado", rotulo: "Postado", variante: "sucesso" },
    { valor: "Entregue", rotulo: "Entregue", variante: "sucesso" },
    { valor: "Cancelado", rotulo: "Cancelado", variante: "esgotado" },
    { valor: "Falha", rotulo: "Falha", variante: "erro" },
    { valor: "AguardandoNota", rotulo: "Aguardando nota fiscal", variante: "alerta" },
];

// ---------------------------------------------------------------------------
// Pagamento — string no detalhe do pedido.
// ---------------------------------------------------------------------------
export const STATUS_PAGAMENTO = [
    { valor: "Pendente", rotulo: "Pendente", variante: "alerta" },
    { valor: "Aprovado", rotulo: "Aprovado", variante: "sucesso" },
    { valor: "Recusado", rotulo: "Recusado", variante: "erro" },
    { valor: "Expirado", rotulo: "Expirado", variante: "esgotado" },
    { valor: "Cancelado", rotulo: "Cancelado", variante: "esgotado" },
    { valor: "Estornado", rotulo: "Estornado", variante: "esgotado" },
];

// ---------------------------------------------------------------------------
// Avaliacao — StatusAvaliacao viaja como NUMERO.
// ---------------------------------------------------------------------------
export const STATUS_AVALIACAO = [
    { valor: 1, rotulo: "Pendente", variante: "alerta" },
    { valor: 2, rotulo: "Aprovada", variante: "sucesso" },
    { valor: 3, rotulo: "Rejeitada", variante: "erro" },
];

export const AVALIACAO_PENDENTE = 1;

/** CaimentoTamanho — o dado que mais reduz devolução em moda. Numero. */
export const CAIMENTO = [
    { valor: 1, rotulo: "Muito pequeno" },
    { valor: 2, rotulo: "Pequeno" },
    { valor: 3, rotulo: "Certo" },
    { valor: 4, rotulo: "Grande" },
    { valor: 5, rotulo: "Muito grande" },
];

// ---------------------------------------------------------------------------
// Cupom — TipoCupom viaja como NUMERO.
// ---------------------------------------------------------------------------
export const TIPO_CUPOM = [
    { valor: 1, rotulo: "Percentual", variante: "contorno" },
    { valor: 2, rotulo: "Valor fixo", variante: "contorno" },
    { valor: 3, rotulo: "Frete grátis", variante: "destaque" },
];

export const TIPO_CUPOM_PERCENTUAL = 1;
export const TIPO_CUPOM_VALOR_FIXO = 2;
export const TIPO_CUPOM_FRETE_GRATIS = 3;

// ---------------------------------------------------------------------------
// Estoque — os rotulos de movimento sao os do catalogo fechado
// (Domain/ReferenceData/MovimentoEstoqueKeys.cs). O `valor` e comparado
// LITERALMENTE no servico: mexer nele quebra a entrada e o ajuste.
// ---------------------------------------------------------------------------
export const MOVIMENTOS_ENTRADA = [
    { valor: "Reabastecimento", rotulo: "Reabastecimento" },
    { valor: "Cadastro inicial", rotulo: "Cadastro inicial" },
    { valor: "Devolucao de cliente", rotulo: "Devolução de cliente" },
];

export const MOVIMENTOS_AJUSTE = [
    { valor: "Ajuste de inventario", rotulo: "Ajuste de inventário" },
    { valor: "Perda/avaria", rotulo: "Perda ou avaria" },
    { valor: "Venda manual", rotulo: "Venda manual" },
];

/** Catalogo completo, para o filtro do extrato. */
export const MOVIMENTOS_ESTOQUE = [
    ...MOVIMENTOS_ENTRADA,
    ...MOVIMENTOS_AJUSTE,
    { valor: "Venda por sistema", rotulo: "Venda por sistema" },
];

// ---------------------------------------------------------------------------
// Consulta generica
// ---------------------------------------------------------------------------

/** Devolve `{ valor, rotulo, variante }` — nunca `undefined`. */
export function descrever(mapa, valor, padrao = "—") {
    const achado = mapa.find((item) => item.valor === valor);
    if (achado) return achado;
    return {
        valor,
        // Valor desconhecido aparece cru: e melhor ver "Xpto" do que um traco
        // que esconde uma divergencia de contrato.
        rotulo: valor == null || valor === "" ? padrao : String(valor),
        variante: "neutro",
    };
}

export function rotularStatusPedido(valor) {
    return descrever(STATUS_PEDIDO, valor).rotulo;
}

export function rotularStatusEnvio(valor) {
    return descrever(STATUS_ENVIO, valor).rotulo;
}
