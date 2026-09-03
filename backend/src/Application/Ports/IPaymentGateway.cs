using Glorific.Application.Models.Pagamento;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de saida do gateway de pagamento.
///
/// Desenhada para o modelo de CHECKOUT WEB HOSPEDADO (o cliente e redirecionado para a pagina do
/// provedor), que e o da InfinitePay hoje. Deliberadamente pequena: duas operacoes, criar e
/// conferir. Trocar de provedor amanha (Pagar.me, Asaas, Mercado Pago) e escrever outro
/// adaptador — nenhum campo aqui e exclusivo da InfinitePay (handle, order_nsu no formato dela,
/// slug) e nenhum tipo HTTP atravessa a fronteira.
///
/// Nao ha metodo de "processar webhook" nesta porta de proposito: webhook nao e integracao de
/// saida, e entrada nao confiavel. O controller le o corpo, monta um WebhookPagamentoInfo e o
/// servico chama <see cref="ConsultarPagamentoAsync"/> para descobrir a verdade.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Identificador do provedor, gravado em pagamentos.provedor (ex.: "infinitepay").
    /// Existe para que um pedido antigo continue sabendo por onde foi pago depois da troca de
    /// gateway — sem isso, conciliacao historica vira arqueologia.
    /// </summary>
    string Nome { get; }

    /// <summary>
    /// Cria o link de pagamento.
    ///
    /// Chamada dentro da transacao do checkout, ANTES do commit: se voltar Sucesso = false, o
    /// orquestrador lanca e o rollback desfaz pedido e reserva de estoque. Nunca existe pedido
    /// comitado sem link.
    /// </summary>
    /// <returns>URL do checkout + o OrderNsu registrado no provedor.</returns>
    Task<CheckoutCriadoInfo> CriarCheckoutAsync(
        CheckoutRequisicaoInfo requisicao,
        CancellationToken ct = default);

    /// <summary>
    /// Confere a transacao direto no provedor. E a UNICA fonte da verdade sobre pagamento.
    ///
    /// Chamada obrigatoria tanto no webhook quanto no retorno do navegador: nenhum dos dois e
    /// confiavel (o webhook da InfinitePay nao tem assinatura e o redirect e um GET que qualquer
    /// um monta). So marcar o pedido como Pago quando esta consulta devolver Aprovado E o valor
    /// bater com o total do pedido.
    /// </summary>
    /// <param name="orderNsu">Nosso identificador de correlacao, obrigatorio.</param>
    /// <param name="transactionNsu">Id da transacao no provedor; pode faltar no primeiro aviso.</param>
    /// <param name="slug">Meio de pagamento informado no retorno (pix, credit_card...).</param>
    Task<ConsultaPagamentoInfo> ConsultarPagamentoAsync(
        string orderNsu,
        string? transactionNsu = null,
        string? slug = null,
        CancellationToken ct = default);
}
