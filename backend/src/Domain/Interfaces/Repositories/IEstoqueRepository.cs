using Glorific.Domain.Entities.Estoque;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Estoque e o unico agregado onde a escrita NAO pode passar por ler, alterar e salvar:
/// read-modify-write do EF perde update sob concorrencia e o resultado e oversell sistematico.
/// Por isso os metodos abaixo sao UPDATE condicional atomico e devolvem bool, nao entidade.
///
/// Cada um traduz um UPDATE com WHERE que ja carrega a regra de negocio, e o "0 linhas afetadas"
/// e a resposta de negocio: nao havia saldo. A implementacao precisa desanexar a entidade do
/// identity map depois, porque UPDATE direto nao atualiza o que o contexto ja rastreava.
/// </summary>
public interface IEstoqueRepository : IBaseRepository<EstoqueVariacao>
{
    Task<EstoqueVariacao?> ObterPorVariacaoAsync(int idVariacao, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EstoqueVariacao>> ObterPorVariacoesAsync(IReadOnlyCollection<int> idsVariacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// UPDATE estoques_variacoes SET quantidade_reservada = quantidade_reservada + q
    /// WHERE id_variacao = id AND (quantidade - quantidade_reservada) greater or equal q.
    /// False significa esgotado naquele tamanho, nao erro de infraestrutura.
    /// </summary>
    Task<bool> TentarReservarAsync(int idVariacao, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Devolve reserva de pagamento expirado ou cancelado. Nao mexe no estoque fisico.</summary>
    Task<bool> LiberarReservaAsync(int idVariacao, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pagamento confirmado: baixa o fisico e a reserva na mesma instrucao. Separar em dois
    /// updates deixaria uma janela em que a peca aparece disponivel e ja foi vendida.
    /// </summary>
    Task<bool> TentarEfetivarVendaAsync(int idVariacao, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Entrada de estoque: reabastecimento, devolucao aprovada, ajuste positivo.</summary>
    Task<bool> RegistrarEntradaAsync(int idVariacao, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Saida sem reserva previa: venda manual, perda, ajuste negativo.</summary>
    Task<bool> TentarBaixarFisicoAsync(int idVariacao, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Alimenta o alerta do painel: disponivel abaixo da quantidade minima.</summary>
    Task<IReadOnlyList<EstoqueVariacao>> ObterAbaixoDoMinimoAsync(CancellationToken cancellationToken = default);
}
