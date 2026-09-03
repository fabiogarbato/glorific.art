using Glorific.Domain.Common;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>
/// Base de todos os repositorios.
///
/// REGRA DURA: nenhum metodo daqui chama SaveChangesAsync. O repositorio registra intencao no
/// ChangeTracker; quem decide o instante do commit e o caso de uso, via IUnitOfWork. O
/// BaseRepository do repo de referencia salvava dentro de Add/Update/Remove e o resultado foi
/// transacao reescrita a mao em sete lugares, cada uma com um tratamento de erro diferente.
///
/// Query() e QueryTracked() sao separados de proposito. Leitura de exibicao nao precisa de
/// snapshot de mudanca: no repo de referencia AsNoTracking nao aparecia uma unica vez e toda
/// listagem carregava a tabela inteira rastreada.
/// </summary>
public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    protected readonly GlorificContext Contexto;

    public BaseRepository(GlorificContext contexto)
    {
        Contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <summary>DbSet cru. Uso interno dos repositorios derivados.</summary>
    protected DbSet<T> Entidades => Contexto.Set<T>();

    /// <inheritdoc />
    public virtual IQueryable<T> Query() => Entidades.AsNoTracking();

    /// <inheritdoc />
    public virtual IQueryable<T> QueryTracked() => Entidades;

    /// <inheritdoc />
    public virtual Task<T?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public virtual Task<T?> ObterParaEdicaoAsync(int id, CancellationToken cancellationToken = default) =>
        QueryTracked().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public virtual Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        Query().AnyAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> ObterPorIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default)
    {
        // Lista vazia nao vira "WHERE id IN ()" nem viagem ao banco.
        if (ids is null || ids.Count == 0)
            return [];

        var distintos = ids.Distinct().ToArray();

        return await Query()
            .Where(x => distintos.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AdicionarAsync(T entidade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entidade);
        await Entidades.AddAsync(entidade, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AdicionarVariosAsync(
        IEnumerable<T> entidades,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entidades);
        await Entidades.AddRangeAsync(entidades, cancellationToken);
    }

    /// <inheritdoc />
    public virtual void Atualizar(T entidade)
    {
        ArgumentNullException.ThrowIfNull(entidade);
        Entidades.Update(entidade);
    }

    /// <inheritdoc />
    public virtual void Remover(T entidade)
    {
        ArgumentNullException.ThrowIfNull(entidade);
        Entidades.Remove(entidade);
    }

    /// <inheritdoc />
    public virtual void RemoverVarios(IEnumerable<T> entidades)
    {
        ArgumentNullException.ThrowIfNull(entidades);
        Entidades.RemoveRange(entidades);
    }

    /// <summary>
    /// Descarta do identity map as instancias que satisfazem o predicado.
    ///
    /// Existe por causa do ExecuteUpdateAsync: ele emite o UPDATE direto no banco e NAO toca no
    /// que o contexto ja rastreava. Sem este descarte, a entidade em memoria continua com o
    /// valor velho e um SaveChanges posterior reescreve o resultado do update atomico —
    /// exatamente o oversell que os UPDATEs condicionais existem para impedir.
    /// </summary>
    protected void DesanexarRastreados<TEntidade>(Func<TEntidade, bool> predicado)
        where TEntidade : class
    {
        // ToList antes de mexer: alterar State modifica a colecao de entries em iteracao.
        var entries = Contexto.ChangeTracker
            .Entries<TEntidade>()
            .Where(e => predicado(e.Entity))
            .ToList();

        foreach (var entry in entries)
            entry.State = EntityState.Detached;
    }
}
