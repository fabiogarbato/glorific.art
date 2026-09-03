using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Busca sempre pelo hash: o token em claro nunca chega ao banco, entao vazamento de dump
    /// nao vira sessao valida. Rastreado de proposito — quem acha o token vai marca-lo como
    /// substituido na sequencia.
    /// </summary>
    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        QueryTracked().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ObterAtivosDoUsuarioAsync(
        int idUsuario,
        DateTime agoraUtc,
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(t => t.IdUsuario == idUsuario && t.RevogadoEm == null && t.ExpiraEm > agoraUtc)
            .OrderByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Revoga a familia inteira em um UPDATE.
    ///
    /// E a resposta a deteccao de reuso: se um token ja substituido reapareceu, a cadeia toda
    /// esta comprometida. Revogar so aquela linha deixaria o atacante com o token seguinte na
    /// mao — por isso o WHERE e por id_familia, nao por id.
    ///
    /// ExecuteUpdateAsync nao passa pelo identity map: os tokens da familia sao desanexados
    /// depois, para nenhum SaveChanges posterior "desrevogar" o que acabou de ser revogado.
    /// </summary>
    public async Task<int> RevogarFamiliaAsync(
        Guid idFamilia,
        DateTime agoraUtc,
        CancellationToken cancellationToken = default)
    {
        var linhas = await Contexto.RefreshTokens
            .Where(t => t.IdFamilia == idFamilia && t.RevogadoEm == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevogadoEm, agoraUtc),
                cancellationToken);

        DesanexarRastreados<RefreshToken>(t => t.IdFamilia == idFamilia);
        return linhas;
    }

    /// <summary>Logout de todos os dispositivos, ou resposta a troca de senha.</summary>
    public async Task<int> RevogarDoUsuarioAsync(
        int idUsuario,
        DateTime agoraUtc,
        CancellationToken cancellationToken = default)
    {
        var linhas = await Contexto.RefreshTokens
            .Where(t => t.IdUsuario == idUsuario && t.RevogadoEm == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevogadoEm, agoraUtc),
                cancellationToken);

        DesanexarRastreados<RefreshToken>(t => t.IdUsuario == idUsuario);
        return linhas;
    }

    /// <summary>
    /// Faxina do worker. DELETE em bloco: linha expirada ha muito tempo nao serve nem para
    /// auditoria e a tabela cresce sem limite se ninguem varrer.
    /// </summary>
    public async Task<int> RemoverExpiradosAsync(
        DateTime anterioresA,
        CancellationToken cancellationToken = default)
    {
        var linhas = await Contexto.RefreshTokens
            .Where(t => t.ExpiraEm < anterioresA)
            .ExecuteDeleteAsync(cancellationToken);

        DesanexarRastreados<RefreshToken>(t => t.ExpiraEm < anterioresA);
        return linhas;
    }
}
