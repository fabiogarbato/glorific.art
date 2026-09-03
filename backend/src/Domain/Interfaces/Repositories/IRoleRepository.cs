using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Os metodos de vinculo ficam aqui porque UsuarioRole tem PK composta e nao herda BaseEntity,
/// entao nao cabe no IBaseRepository generico.
/// </summary>
public interface IRoleRepository : IBaseRepository<Role>
{
    Task<Role?> ObterPorNomeAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>So os nomes: e exatamente o que vira claim role no JWT.</summary>
    Task<IReadOnlyList<string>> ObterNomesDoUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    Task<UsuarioRole?> ObterVinculoAsync(int idUsuario, int idRole, CancellationToken cancellationToken = default);

    Task ConcederAsync(UsuarioRole vinculo, CancellationToken cancellationToken = default);

    void Revogar(UsuarioRole vinculo);
}
