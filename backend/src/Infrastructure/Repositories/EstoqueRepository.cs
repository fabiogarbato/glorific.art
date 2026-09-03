using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>
/// Estoque e o unico agregado onde a escrita NAO passa por ler, alterar e salvar.
///
/// O motivo e concreto: dois checkouts simultaneos leem quantidade 1, os dois acham que da,
/// os dois gravam 0 reservado + 1, e a loja vende duas vezes a mesma peca. Read-modify-write
/// do EF perde update sob concorrencia e o resultado e oversell sistematico. Aqui cada
/// operacao e UM UPDATE com WHERE que ja carrega a regra: o banco decide, em uma unica
/// instrucao atomica, se havia saldo. "0 linhas afetadas" nao e erro de infraestrutura, e a
/// resposta de negocio "nao tem".
///
/// ARMADILHA DO ExecuteUpdateAsync: ele nao passa pelo ChangeTracker. Se a mesma
/// EstoqueVariacao ja estava rastreada nesta unidade de trabalho, a instancia em memoria
/// continua com o valor antigo e o SaveChanges do caso de uso reescreveria o resultado do
/// UPDATE atomico. Por isso todo metodo atomico daqui desanexa a linha afetada logo depois;
/// quem precisar do saldo novo tem que reconsultar (ObterPorVariacaoAsync).
/// </summary>
public sealed class EstoqueRepository : BaseRepository<EstoqueVariacao>, IEstoqueRepository
{
    private readonly IClock _relogio;

    public EstoqueRepository(GlorificContext contexto, IClock relogio) : base(contexto)
    {
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    public Task<EstoqueVariacao?> ObterPorVariacaoAsync(
        int idVariacao,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.IdVariacao == idVariacao, cancellationToken);

    public async Task<IReadOnlyList<EstoqueVariacao>> ObterPorVariacoesAsync(
        IReadOnlyCollection<int> idsVariacao,
        CancellationToken cancellationToken = default)
    {
        if (idsVariacao is null || idsVariacao.Count == 0)
            return [];

        var ids = idsVariacao.Distinct().ToArray();

        return await Query()
            .Where(e => ids.Contains(e.IdVariacao))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// UPDATE estoques_variacoes SET quantidade_reservada = quantidade_reservada + q
    /// WHERE id_variacao = @id AND (quantidade - quantidade_reservada) >= q.
    ///
    /// False significa esgotado naquele tamanho. O saldo disponivel e recalculado pelo banco
    /// dentro da propria instrucao: nenhum valor lido antes participa da decisao.
    /// </summary>
    public async Task<bool> TentarReservarAsync(
        int idVariacao,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return false;

        var agora = _relogio.UtcNow;

        var linhas = await Contexto.EstoquesVariacoes
            .Where(e => e.IdVariacao == idVariacao
                        && (e.Quantidade - e.QuantidadeReservada) >= quantidade)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.QuantidadeReservada, e => e.QuantidadeReservada + quantidade)
                    .SetProperty(e => e.DataUltimaMovimentacao, agora),
                cancellationToken);

        DesanexarEstoque(idVariacao);
        return linhas > 0;
    }

    /// <summary>
    /// Devolve reserva de pagamento expirado ou cancelado. Nao mexe no fisico.
    /// O WHERE reservada >= q impede reserva negativa vinda de liberacao duplicada — cenario
    /// real quando o webhook de expiracao e reentregue.
    /// </summary>
    public async Task<bool> LiberarReservaAsync(
        int idVariacao,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return false;

        var agora = _relogio.UtcNow;

        var linhas = await Contexto.EstoquesVariacoes
            .Where(e => e.IdVariacao == idVariacao && e.QuantidadeReservada >= quantidade)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.QuantidadeReservada, e => e.QuantidadeReservada - quantidade)
                    .SetProperty(e => e.DataUltimaMovimentacao, agora),
                cancellationToken);

        DesanexarEstoque(idVariacao);
        return linhas > 0;
    }

    /// <summary>
    /// Pagamento confirmado: baixa o fisico E a reserva na MESMA instrucao.
    ///
    /// Dois updates separados deixariam uma janela em que a reserva ja caiu e o fisico ainda
    /// nao — nessa fresta a peca aparece disponivel na vitrine e ja foi vendida.
    /// </summary>
    public async Task<bool> TentarEfetivarVendaAsync(
        int idVariacao,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return false;

        var agora = _relogio.UtcNow;

        var linhas = await Contexto.EstoquesVariacoes
            .Where(e => e.IdVariacao == idVariacao
                        && e.QuantidadeReservada >= quantidade
                        && e.Quantidade >= quantidade)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Quantidade, e => e.Quantidade - quantidade)
                    .SetProperty(e => e.QuantidadeReservada, e => e.QuantidadeReservada - quantidade)
                    .SetProperty(e => e.DataUltimaMovimentacao, agora),
                cancellationToken);

        DesanexarEstoque(idVariacao);
        return linhas > 0;
    }

    /// <summary>Entrada: reabastecimento, devolucao aprovada, ajuste positivo.</summary>
    public async Task<bool> RegistrarEntradaAsync(
        int idVariacao,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return false;

        var agora = _relogio.UtcNow;

        var linhas = await Contexto.EstoquesVariacoes
            .Where(e => e.IdVariacao == idVariacao)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Quantidade, e => e.Quantidade + quantidade)
                    .SetProperty(e => e.DataUltimaMovimentacao, agora),
                cancellationToken);

        DesanexarEstoque(idVariacao);
        return linhas > 0;
    }

    /// <summary>
    /// Saida sem reserva previa: venda manual, perda, ajuste negativo.
    /// O WHERE exige que sobre saldo LIVRE — baixar fisico por cima de reserva alheia
    /// derrubaria um pedido ja pago de outro cliente.
    /// </summary>
    public async Task<bool> TentarBaixarFisicoAsync(
        int idVariacao,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return false;

        var agora = _relogio.UtcNow;

        var linhas = await Contexto.EstoquesVariacoes
            .Where(e => e.IdVariacao == idVariacao
                        && (e.Quantidade - e.QuantidadeReservada) >= quantidade)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Quantidade, e => e.Quantidade - quantidade)
                    .SetProperty(e => e.DataUltimaMovimentacao, agora),
                cancellationToken);

        DesanexarEstoque(idVariacao);
        return linhas > 0;
    }

    /// <summary>
    /// Alerta do painel: disponivel abaixo da quantidade minima. Disponivel e propriedade
    /// calculada em memoria, entao a conta vai explicita no WHERE para o banco resolver.
    /// </summary>
    public async Task<IReadOnlyList<EstoqueVariacao>> ObterAbaixoDoMinimoAsync(
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(e => e.QuantidadeMinima > 0
                        && (e.Quantidade - e.QuantidadeReservada) < e.QuantidadeMinima
                        && e.Variacao.Ativo
                        && e.Variacao.Produto.Ativo)
            .Include(e => e.Variacao).ThenInclude(v => v.Produto)
            .Include(e => e.Variacao).ThenInclude(v => v.Tamanho)
            .Include(e => e.Variacao).ThenInclude(v => v.Cor)
            .OrderBy(e => e.Quantidade - e.QuantidadeReservada)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Descarta a linha do identity map depois do UPDATE direto. Ver o comentario da classe:
    /// sem isso o contexto guarda um saldo desatualizado e pode reescreve-lo no SaveChanges.
    /// </summary>
    private void DesanexarEstoque(int idVariacao) =>
        DesanexarRastreados<EstoqueVariacao>(e => e.IdVariacao == idVariacao);
}
