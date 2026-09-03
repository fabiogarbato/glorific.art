using Glorific.Domain.Entities.Pedidos;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IPedidoRepository : IBaseRepository<Pedido>
{
    Task<Pedido?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default);

    Task<Pedido?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Com itens, pagamento, envio e historico. Le sempre ignorando o filtro de soft delete:
    /// pedido antigo tem item de produto desativado, e sem isso o recibo abre sem as linhas.
    /// </summary>
    Task<Pedido?> ObterCompletoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sempre filtrando por usuario. Buscar so por Id e depois comparar o dono em memoria vaza
    /// existencia do pedido alheio; aqui o filtro esta na consulta e o resultado e 404.
    /// </summary>
    Task<Pedido?> ObterDoUsuarioAsync(int idUsuario, string uuid, CancellationToken cancellationToken = default);

    IQueryable<Pedido> QueryDoUsuario(int idUsuario);

    /// <summary>Proximo sequencial humano do ano, no formato GA-2026-000137.</summary>
    Task<string> GerarProximoNumeroAsync(int ano, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fila do worker de expiracao: aguardando pagamento com prazo vencido. Cancelar libera a
    /// reserva de estoque, que e o motivo de o worker existir.
    /// </summary>
    Task<IReadOnlyList<Pedido>> ObterAguardandoPagamentoVencidosAsync(DateTime agoraUtc, int limite, CancellationToken cancellationToken = default);

    Task RegistrarHistoricoAsync(PedidoHistorico historico, CancellationToken cancellationToken = default);
}
