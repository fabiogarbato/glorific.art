using Glorific.Domain.Entities.Clientes;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IEnderecoRepository : IBaseRepository<Endereco>
{
    Task<IReadOnlyList<Endereco>> ObterDoUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sempre filtrando por usuario. Buscar so por Id abriria IDOR: o cliente enviaria o Id do
    /// endereco de outra pessoa no checkout e a etiqueta sairia com o endereco dela.
    /// </summary>
    Task<Endereco?> ObterDoUsuarioAsync(int idUsuario, int idEndereco, CancellationToken cancellationToken = default);

    Task<Endereco?> ObterPrincipalAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>UPDATE em bloco antes de marcar o novo principal: so pode existir um por usuario.</summary>
    Task DesmarcarPrincipaisAsync(int idUsuario, CancellationToken cancellationToken = default);
}
