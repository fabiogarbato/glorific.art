namespace Glorific.Application.Ports;

/// <summary>
/// Porta pro provedor de IA que escreve o TEXTO ALTERNATIVO (alt text) de uma imagem do acervo —
/// diferente da descrição de produto: é uma frase curta, objetiva, pensada pra leitor de tela e
/// busca, não pra vender a peça. Mesmo adaptador (OpenAI) da descrição de produto, prompt e
/// formato de saída são outros.
/// </summary>
public interface IGeradorTextoAlternativo
{
    Task<string> GerarAsync(TextoAlternativoPedido pedido, CancellationToken ct = default);
}

/// <summary>
/// Tudo que o adaptador precisa pra escrever o alt text. A Application busca a imagem e alguns
/// textos alternativos já cadastrados (referência de padrão); o adaptador só conhece IA.
/// </summary>
public sealed record TextoAlternativoPedido
{
    public required byte[] ImagemBytes { get; init; }

    public required string ImagemContentType { get; init; }

    /// <summary>Alt texts de outras imagens do acervo, usados como referência de padrão/formato.</summary>
    public IReadOnlyList<string> ExemplosExistentes { get; init; } = [];
}
