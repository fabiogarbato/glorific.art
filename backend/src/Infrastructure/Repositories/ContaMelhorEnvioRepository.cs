using Glorific.Domain.Entities.Integracoes;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>Mesma forma de ConfiguracaoLojaRepository: linha unica, sem listar/remover.</summary>
public sealed class ContaMelhorEnvioRepository : IContaMelhorEnvioRepository
{
    private readonly GlorificContext _contexto;

    public ContaMelhorEnvioRepository(GlorificContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<ContaMelhorEnvio?> ObterAsync(CancellationToken cancellationToken = default) =>
        _contexto.ContasMelhorEnvio
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ContaMelhorEnvio?> ObterParaEdicaoAsync(CancellationToken cancellationToken = default) =>
        _contexto.ContasMelhorEnvio
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AdicionarAsync(
        ContaMelhorEnvio conta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conta);
        await _contexto.ContasMelhorEnvio.AddAsync(conta, cancellationToken);
    }
}
