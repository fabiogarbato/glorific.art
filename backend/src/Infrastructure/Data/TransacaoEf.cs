using Glorific.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// Adaptador entre a transacao do EF e o contrato IDbTransacao do Domain.
///
/// Existe para o Application declarar "isto tudo acontece junto" sem referenciar EF.
/// O descarte sem commit faz rollback — comportamento do IDbContextTransaction, que e
/// exatamente o esperado num using que estourou excecao no meio.
/// </summary>
internal sealed class TransacaoEf : IDbTransacao
{
    private readonly IDbContextTransaction _transacao;

    public TransacaoEf(IDbContextTransaction transacao) => _transacao = transacao;

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _transacao.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => _transacao.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transacao.DisposeAsync();
}
