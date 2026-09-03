using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>
/// Os metodos de vinculo ficam aqui porque UsuarioRole tem PK composta e nao herda BaseEntity,
/// entao nao cabe no IBaseRepository generico.
/// </summary>
public sealed class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Role?> ObterPorNomeAsync(string nome, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(r => r.Nome == nome, cancellationToken);

    /// <summary>
    /// So os nomes: e exatamente o que vira claim role no JWT. Carregar as entidades inteiras
    /// para ler uma string cada e o tipo de desperdicio que acontece em todo login.
    /// </summary>
    public async Task<IReadOnlyList<string>> ObterNomesDoUsuarioAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        await Contexto.UsuariosRoles
            .AsNoTracking()
            .Where(ur => ur.IdUsuario == idUsuario)
            .Select(ur => ur.Role.Nome)
            .OrderBy(nome => nome)
            .ToListAsync(cancellationToken);

    /// <summary>Rastreado: quem busca o vinculo esta prestes a revoga-lo.</summary>
    public Task<UsuarioRole?> ObterVinculoAsync(
        int idUsuario,
        int idRole,
        CancellationToken cancellationToken = default) =>
        Contexto.UsuariosRoles
            .FirstOrDefaultAsync(ur => ur.IdUsuario == idUsuario && ur.IdRole == idRole, cancellationToken);

    public async Task ConcederAsync(UsuarioRole vinculo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vinculo);
        await Contexto.UsuariosRoles.AddAsync(vinculo, cancellationToken);
    }

    public void Revogar(UsuarioRole vinculo)
    {
        ArgumentNullException.ThrowIfNull(vinculo);
        Contexto.UsuariosRoles.Remove(vinculo);
    }
}
