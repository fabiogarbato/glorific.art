namespace Glorific.Application.DTO.Painel;

/// <summary>
/// SKU no alerta de reposicao.
///
/// Disponivel e Quantidade menos QuantidadeReservada, nao a quantidade fisica: peca reservada por
/// checkout aguardando pagamento nao pode ser vendida de novo, e listar o fisico faria o alerta
/// mentir justamente na hora de maior giro.
/// </summary>
public sealed record DashboardEstoqueBaixoDto : ResponseDto
{
    public int IdVariacao { get; init; }

    public string Sku { get; init; } = string.Empty;

    public int IdProduto { get; init; }

    public string NomeProduto { get; init; } = string.Empty;

    public string Tamanho { get; init; } = string.Empty;

    public string Cor { get; init; } = string.Empty;

    public int Quantidade { get; init; }

    public int QuantidadeReservada { get; init; }

    public int Disponivel { get; init; }

    public int QuantidadeMinima { get; init; }
}
