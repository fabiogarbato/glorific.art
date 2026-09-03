using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Estoque;

/// <summary>Lookup do tipo de movimento. Resolvido por chave textual mais cache em memoria.</summary>
public class MovimentoEstoque : BaseEntity
{
    public required string Nome { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Positivo entrada, negativo saida, zero neutro (reserva e liberacao nao mexem no fisico).</summary>
    public int Sinal { get; set; }

    public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = [];
}
