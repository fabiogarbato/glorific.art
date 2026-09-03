namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Linha do carrinho vista pelo cupom.
///
/// So carrega o que a regra de desconto precisa. O cupom nao le carrinho nem pedido de proposito:
/// ele e chamado tanto pela previa do carrinho (nada persistido) quanto pelo checkout (pedido
/// ainda em memoria), e depender de um agregado especifico amarraria as duas chamadas.
///
/// TotalLinhaCentavos e o valor JA calculado pelo chamador (quantidade x preco unitario menos
/// desconto de linha). Recalcular aqui abriria a chance de o cupom trabalhar sobre um numero
/// diferente do que o cliente esta vendo na tela.
/// </summary>
public sealed record CupomItemContexto
{
    public int IdProduto { get; init; }

    public int Quantidade { get; init; }

    public int TotalLinhaCentavos { get; init; }
}
