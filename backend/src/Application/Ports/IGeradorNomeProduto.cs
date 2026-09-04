namespace Glorific.Application.Ports;

/// <summary>
/// Porta pro provedor de IA que sugere o NOME da peça a partir da foto — a mesma leitura
/// interpretativa da estampa usada na descrição (ver IGeradorDescricaoProduto), mas condensada
/// num nome de produto curto e vendável.
/// </summary>
public interface IGeradorNomeProduto
{
    Task<string> GerarAsync(NomeProdutoPedido pedido, CancellationToken ct = default);
}

public sealed record NomeProdutoPedido
{
    public required byte[] ImagemBytes { get; init; }

    public required string ImagemContentType { get; init; }

    public string? CategoriaNome { get; init; }

    /// <summary>Nomes de outras peças já cadastradas, só como referência de padrão/tamanho.</summary>
    public IReadOnlyList<string> NomesExemplo { get; init; } = [];
}
