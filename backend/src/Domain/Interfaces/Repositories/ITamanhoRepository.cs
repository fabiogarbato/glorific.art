using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ITamanhoRepository : IBaseRepository<Tamanho>
{
    Task<Tamanho?> ObterPorCodigoAsync(GradeTamanho grade, string codigo, CancellationToken cancellationToken = default);

    /// <summary>Ordenado por Ordem, nunca alfabeticamente: senao GG aparece antes de P no seletor.</summary>
    Task<IReadOnlyList<Tamanho>> ObterAtivosOrdenadosAsync(GradeTamanho? grade = null, CancellationToken cancellationToken = default);

    Task<bool> CodigoEmUsoAsync(GradeTamanho grade, string codigo, int? idIgnorar = null, CancellationToken cancellationToken = default);
}
