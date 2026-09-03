using Glorific.Application.DTO.Checkout;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Orquestrador do checkout. Nao herda de IGenericService de proposito: checkout nao e CRUD de
/// um agregado, e uma transacao que atravessa carrinho, estoque, cupom, pedido, pagamento e um
/// parceiro externo.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Fecha o pedido: revalida preco, RECOTA o frete no servidor, consome o cupom, reserva o
    /// estoque item a item, grava pedido com todos os snapshots e cria a cobranca no gateway.
    /// Tudo em uma transacao — se o gateway nao devolver link, nada disso fica gravado.
    /// </summary>
    /// <param name="usuarioUuid">Vem do claim sub do token. O corpo nunca carrega usuario.</param>
    Task<CheckoutCriadoResponseDto> FinalizarAsync(
        string usuarioUuid,
        CheckoutRequestDto requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alvo do polling da tela "aguardando pagamento". Le o estado local — nao consulta o
    /// gateway: quem confere no gateway e o fluxo de confirmacao, e deixar o polling do
    /// navegador disparar payment_check daria a qualquer um um gerador de carga gratuito.
    /// </summary>
    Task<CheckoutStatusResponseDto> ConsultarStatusAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default);
}
