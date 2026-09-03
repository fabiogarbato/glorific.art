using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class TamanhoRepository : BaseRepository<Tamanho>, ITamanhoRepository
{
    public TamanhoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// O codigo so e unico DENTRO da grade: "38" existe na grade numerica e na infantil, e sao
    /// tamanhos diferentes. Por isso a busca leva sempre as duas partes da chave.
    /// </summary>
    public Task<Tamanho?> ObterPorCodigoAsync(
        GradeTamanho grade,
        string codigo,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(t => t.Grade == grade && t.Codigo == codigo, cancellationToken);

    /// <summary>
    /// Ordenado por Ordem, nunca alfabeticamente: senao GG aparece antes de P no seletor.
    /// Grade nula devolve todas as grades, ja separadas por grade e depois por ordem.
    /// </summary>
    public async Task<IReadOnlyList<Tamanho>> ObterAtivosOrdenadosAsync(
        GradeTamanho? grade = null,
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(t => t.Ativo && (grade == null || t.Grade == grade))
            .OrderBy(t => t.Grade)
            .ThenBy(t => t.Ordem)
            .ThenBy(t => t.Codigo)
            .ToListAsync(cancellationToken);

    public Task<bool> CodigoEmUsoAsync(
        GradeTamanho grade,
        string codigo,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default) =>
        Query().AnyAsync(
            t => t.Grade == grade && t.Codigo == codigo && (idIgnorar == null || t.Id != idIgnorar),
            cancellationToken);
}
