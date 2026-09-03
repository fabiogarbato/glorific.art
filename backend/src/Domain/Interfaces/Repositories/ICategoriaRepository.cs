using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ICategoriaRepository : IBaseRepository<Categoria>
{
    Task<Categoria?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugEmUsoAsync(string slug, int? idIgnorar = null, CancellationToken cancellationToken = default);

    /// <summary>Raizes com as filhas carregadas: o menu do site inteiro em uma consulta.</summary>
    Task<IReadOnlyList<Categoria>> ObterArvoreAsync(bool somenteHabilitadas = true, CancellationToken cancellationToken = default);

    /// <summary>Barra a exclusao antes de o Restrict do banco virar erro cru na tela do admin.</summary>
    Task<bool> PossuiVinculosAsync(int idCategoria, CancellationToken cancellationToken = default);
}
