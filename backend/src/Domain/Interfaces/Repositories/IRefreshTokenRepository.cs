using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
{
    /// <summary>Busca sempre pelo hash: o token em claro nunca chega ao banco.</summary>
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> ObterAtivosDoUsuarioAsync(int idUsuario, DateTime agoraUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga a familia inteira de uma vez. E a resposta a deteccao de reuso: se um token ja
    /// substituido reapareceu, a cadeia toda esta comprometida e revogar so aquela linha deixa
    /// o atacante com o token seguinte na mao.
    /// </summary>
    Task<int> RevogarFamiliaAsync(Guid idFamilia, DateTime agoraUtc, CancellationToken cancellationToken = default);

    Task<int> RevogarDoUsuarioAsync(int idUsuario, DateTime agoraUtc, CancellationToken cancellationToken = default);

    /// <summary>Faxina do worker: linha expirada ha muito tempo nao serve nem para auditoria.</summary>
    Task<int> RemoverExpiradosAsync(DateTime anterioresA, CancellationToken cancellationToken = default);
}
