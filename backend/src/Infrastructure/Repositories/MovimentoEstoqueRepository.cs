using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Domain.ReferenceData;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>
/// Lookup dos tipos de movimento mais o ledger de movimentacoes.
///
/// O tipo e resolvido por chave textual, nunca por Id fixo no codigo: seed rodando em ordem
/// diferente entre ambientes daria Ids diferentes e um "Perda/avaria" de homologacao viraria
/// "Reabastecimento" em producao.
/// </summary>
public sealed class MovimentoEstoqueRepository : BaseRepository<MovimentoEstoque>, IMovimentoEstoqueRepository
{
    /// <summary>
    /// Cache do escopo da requisicao. Nao e estatico de proposito: um cache de processo
    /// atravessaria bases diferentes (teste, homologacao) com os Ids de outra.
    /// </summary>
    private readonly Dictionary<string, int> _idsPorChave = [];

    public MovimentoEstoqueRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<MovimentoEstoque?> ObterPorChaveAsync(
        MovimentoEstoqueKey chave,
        CancellationToken cancellationToken = default)
    {
        var nome = chave.Value;

        return Query().FirstOrDefaultAsync(m => m.Nome == nome, cancellationToken);
    }

    /// <summary>
    /// Resolve so o Id, que e o que a movimentacao precisa gravar.
    ///
    /// Chave ausente e erro de seed, nao caminho de negocio: falhar alto aqui e melhor que
    /// gravar movimentacao apontando para o tipo errado e descobrir no inventario.
    /// </summary>
    public async Task<int> ObterIdPorChaveAsync(
        MovimentoEstoqueKey chave,
        CancellationToken cancellationToken = default)
    {
        var nome = chave.Value;

        if (_idsPorChave.TryGetValue(nome, out var idEmCache))
            return idEmCache;

        var id = await Query()
            .Where(m => m.Nome == nome)
            .Select(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (id == 0)
            throw new InvalidOperationException(
                $"Movimento de estoque '{nome}' nao encontrado. O seed de dados de referencia nao rodou nesta base.");

        _idsPorChave[nome] = id;
        return id;
    }

    public async Task<IReadOnlyList<MovimentoEstoque>> ObterTodosAsync(
        CancellationToken cancellationToken = default) =>
        await Query()
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

    /// <summary>O ledger e append-only: nao existe atualizar nem remover movimentacao.</summary>
    public async Task RegistrarMovimentacaoAsync(
        MovimentacaoEstoque movimentacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movimentacao);
        await Contexto.MovimentacoesEstoque.AddAsync(movimentacao, cancellationToken);
    }

    /// <summary>
    /// Base do extrato do painel. IQueryable para o caso de uso filtrar por periodo, variacao ou
    /// tipo e paginar server-side — o ledger cresce para sempre e nunca deve ser materializado
    /// inteiro. IgnoreQueryFilters: movimentacao de produto desativado continua no extrato,
    /// senao o saldo do relatorio nao fecha com o do estoque.
    /// </summary>
    public IQueryable<MovimentacaoEstoque> QueryMovimentacoes() =>
        Contexto.MovimentacoesEstoque
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderByDescending(m => m.DataMovimentacao)
            .ThenByDescending(m => m.Id);
}
