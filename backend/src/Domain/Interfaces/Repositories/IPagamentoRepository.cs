using Glorific.Domain.Entities.Pedidos;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IPagamentoRepository : IBaseRepository<Pagamento>
{
    Task<Pagamento?> ObterPorPedidoAsync(int idPedido, CancellationToken cancellationToken = default);

    /// <summary>
    /// O webhook chega ora com o id do pedido no gateway, ora com o id da cobranca. Procurar por
    /// um so deixa evento orfao esperando um pagamento que existe.
    /// </summary>
    Task<Pagamento?> ObterPorProviderOrderIdAsync(string providerOrderId, CancellationToken cancellationToken = default);

    Task<Pagamento?> ObterPorProviderChargeIdAsync(string providerChargeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotencia de webhook feita no banco. Devolve false quando o ProviderEventId ja existe,
    /// e a reentrega vira 200 imediato em vez de reprocessamento. Um if no handler nao cobre
    /// eventos de tipos diferentes chegando fora de ordem.
    /// </summary>
    Task<bool> TentarRegistrarEventoAsync(PagamentoEvento evento, CancellationToken cancellationToken = default);

    /// <summary>Fila do worker: o webhook so grava e responde, o processamento pesado vem depois.</summary>
    Task<IReadOnlyList<PagamentoEvento>> ObterEventosNaoProcessadosAsync(int limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pagamento>> ObterExpiradosAsync(DateTime agoraUtc, int limite, CancellationToken cancellationToken = default);
}
