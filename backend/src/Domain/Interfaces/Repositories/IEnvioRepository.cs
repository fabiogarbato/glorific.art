using Glorific.Domain.Entities.Pedidos;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IEnvioRepository : IBaseRepository<Envio>
{
    Task<Envio?> ObterPorPedidoAsync(int idPedido, CancellationToken cancellationToken = default);

    Task<Envio?> ObterPorMeOrderIdAsync(string meOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fila do worker de etiquetas: status pendente, no carrinho ou comprado, com a proxima
    /// tentativa ja vencida. O backoff vem de EnvioRetryPolicy.
    /// </summary>
    Task<IReadOnlyList<Envio>> ObterPendentesAsync(DateTime agoraUtc, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim atomico da linha antes de chamar o Melhor Envio. Sem ele, o worker e a acao manual
    /// do admin compram etiqueta ao mesmo tempo e a segunda e cobrada sem ser usada.
    /// False significa que outro processo pegou primeiro.
    /// </summary>
    Task<bool> TentarReivindicarAsync(int idEnvio, DateTime agoraUtc, CancellationToken cancellationToken = default);

    Task RegistrarEventoAsync(EnvioEvento evento, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnvioEvento>> ObterEventosAsync(int idEnvio, CancellationToken cancellationToken = default);
}
