using Glorific.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// Implementacao EF da porta de materializacao assincrona.
///
/// E o unico lugar onde ToListAsync/CountAsync sao chamados em nome do Application — e por
/// isso que a camada de aplicacao consegue compor IQueryable sem referenciar EF.
///
/// O teste do provider existe por causa dos testes de unidade: uma lista em memoria virada
/// IQueryable NAO tem provider assincrono, e as extensoes do EF lancam
/// "The source IQueryable doesn't implement IAsyncQueryProvider" em vez de simplesmente rodar.
/// Aqui o caminho sincrono e o fallback explicito, nao um acidente.
/// </summary>
public sealed class ConsultaAssincronaEf : IConsultaAssincrona
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ListarAsync<T>(
        IQueryable<T> consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (!Assincrono(consulta))
            return [.. consulta];

        return await consulta.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ContarAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (!Assincrono(consulta))
            return consulta.Count();

        return await consulta.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> PrimeiroOuPadraoAsync<T>(
        IQueryable<T> consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (!Assincrono(consulta))
            return consulta.FirstOrDefault();

        return await consulta.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AlgumAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (!Assincrono(consulta))
            return consulta.Any();

        return await consulta.AnyAsync(cancellationToken);
    }

    private static bool Assincrono<T>(IQueryable<T> consulta) => consulta.Provider is IAsyncQueryProvider;
}
