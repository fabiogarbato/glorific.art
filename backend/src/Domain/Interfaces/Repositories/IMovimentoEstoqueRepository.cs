using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.ReferenceData;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Lookup dos tipos de movimento mais o ledger de movimentacoes.
///
/// O tipo e resolvido por chave textual e nao por Id fixo no codigo: seed rodando em ordem
/// diferente entre ambientes daria Ids diferentes e um "Perda/avaria" de homologacao viraria
/// "Reabastecimento" em producao.
/// </summary>
public interface IMovimentoEstoqueRepository : IBaseRepository<MovimentoEstoque>
{
    Task<MovimentoEstoque?> ObterPorChaveAsync(MovimentoEstoqueKey chave, CancellationToken cancellationToken = default);

    /// <summary>Resolve so o Id, que e o que a movimentacao precisa gravar.</summary>
    Task<int> ObterIdPorChaveAsync(MovimentoEstoqueKey chave, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovimentoEstoque>> ObterTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>O ledger e append-only: nao existe atualizar nem remover movimentacao.</summary>
    Task RegistrarMovimentacaoAsync(MovimentacaoEstoque movimentacao, CancellationToken cancellationToken = default);

    IQueryable<MovimentacaoEstoque> QueryMovimentacoes();
}
