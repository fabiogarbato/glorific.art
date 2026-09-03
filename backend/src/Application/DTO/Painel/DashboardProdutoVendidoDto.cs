namespace Glorific.Application.DTO.Painel;

/// <summary>
/// Linha do ranking de mais vendidos.
///
/// O nome vem do SNAPSHOT gravado no item do pedido, nao do catalogo atual: renomear a peca no
/// admin nao pode reescrever o relatorio do mes passado, e produto desativado precisa continuar
/// aparecendo no ranking do periodo em que vendeu.
/// </summary>
public sealed record DashboardProdutoVendidoDto : ResponseDto
{
    public int IdProduto { get; init; }

    public string NomeProduto { get; init; } = string.Empty;

    public int QuantidadeVendida { get; init; }

    public int TotalCentavos { get; init; }
}
