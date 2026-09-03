namespace Glorific.Domain.Interfaces;

/// <summary>
/// Transacao explicita, expressa sem nenhum tipo de EF — e o que permite ao Domain declarar
/// "isto tudo acontece junto" sem o Application precisar referenciar Infrastructure.
/// Herda IAsyncDisposable para o descarte sem commit fazer rollback no using.
/// </summary>
public interface IDbTransacao : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
