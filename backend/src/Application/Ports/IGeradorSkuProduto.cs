namespace Glorific.Application.Ports;

/// <summary>
/// Porta pro provedor de IA que sugere o SKU base da peça. Diferente da descrição e do nome,
/// não é tarefa visual — é reconhecer o PADRÃO de código já usado nos outros produtos (prefixo
/// de categoria, abreviações, separadores) e aplicá-lo a esta peça. Por isso o pedido não carrega
/// imagem.
/// </summary>
public interface IGeradorSkuProduto
{
    Task<string> GerarAsync(SkuProdutoPedido pedido, CancellationToken ct = default);
}

public sealed record SkuProdutoPedido
{
    public required string NomeProduto { get; init; }

    public string? CategoriaNome { get; init; }

    /// <summary>Pares "nome → SKU" de outras peças já cadastradas, pra IA aprender o padrão.</summary>
    public IReadOnlyList<string> ExemplosSku { get; init; } = [];
}
