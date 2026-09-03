using Glorific.Domain.Common;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Contrato generico de persistencia.
///
/// REGRA DURA: nenhum metodo aqui salva. Quem chama SaveChanges e o caso de uso, via IUnitOfWork.
/// O repositorio so registra intencao. O BaseRepository do repo de referencia salvava dentro de
/// Add/Update/Remove, e o resultado foi transacao reescrita a mao em sete lugares.
///
/// Query e QueryTracked sao separados de proposito: AsNoTracking nao aparecia uma unica vez no
/// repo de referencia e toda listagem carregava a tabela inteira rastreada. Aqui leitura e sem
/// rastreamento por padrao, e quem vai escrever pede explicitamente.
/// </summary>
public interface IBaseRepository<T> where T : BaseEntity
{
    /// <summary>Leitura sem rastreamento. Ponto de partida de toda consulta de exibicao.</summary>
    IQueryable<T> Query();

    /// <summary>Leitura rastreada, para quando a entidade lida sera alterada em seguida.</summary>
    IQueryable<T> QueryTracked();

    Task<T?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Rastreado: use quando a entidade sera alterada na sequencia.</summary>
    Task<T?> ObterParaEdicaoAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ObterPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task AdicionarAsync(T entidade, CancellationToken cancellationToken = default);

    Task AdicionarVariosAsync(IEnumerable<T> entidades, CancellationToken cancellationToken = default);

    void Atualizar(T entidade);

    void Remover(T entidade);

    void RemoverVarios(IEnumerable<T> entidades);
}
