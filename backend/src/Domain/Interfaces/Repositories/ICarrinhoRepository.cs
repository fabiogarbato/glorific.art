using Glorific.Domain.Entities.Carrinho;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ICarrinhoRepository : IBaseRepository<Carrinho>
{
    /// <summary>Com itens, variacao, produto e estoque: e o que a tela do carrinho precisa inteira.</summary>
    Task<Carrinho?> ObterAbertoDoUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Carrinho do visitante anonimo, achado pelo cookie de sessao.</summary>
    Task<Carrinho?> ObterAbertoPorSessaoAsync(string chaveSessao, CancellationToken cancellationToken = default);

    Task<Carrinho?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default);

    Task<CarrinhoItem?> ObterItemAsync(int idCarrinho, int idVariacao, CancellationToken cancellationToken = default);

    Task AdicionarItemAsync(CarrinhoItem item, CancellationToken cancellationToken = default);

    void RemoverItem(CarrinhoItem item);

    /// <summary>
    /// Fila do worker de abandono. O carrinho nao reserva estoque, entao expirar e so mudar o
    /// status e liberar o slot do indice parcial de carrinho aberto por usuario.
    /// </summary>
    Task<IReadOnlyList<Carrinho>> ObterExpiradosAsync(DateTime agoraUtc, int limite, CancellationToken cancellationToken = default);
}
