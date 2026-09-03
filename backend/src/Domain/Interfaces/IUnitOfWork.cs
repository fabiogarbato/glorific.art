namespace Glorific.Domain.Interfaces;

/// <summary>
/// Quem salva e o caso de uso, nunca o repositorio.
///
/// O BaseRepository do repo de referencia chamava SaveChangesAsync dentro de Add/Update/Remove.
/// O efeito: nao da para compor duas escritas numa unidade, e a transacao acabou reescrita a mao
/// em sete lugares — cada um com um tratamento de erro diferente.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persiste tudo que foi rastreado. Devolve o numero de linhas afetadas.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transacao explicita para o que precisa ser atomico entre agregados — checkout reserva
    /// estoque, consome cupom e cria pedido, e ou tudo acontece ou nada acontece.
    /// </summary>
    Task<IDbTransacao> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
