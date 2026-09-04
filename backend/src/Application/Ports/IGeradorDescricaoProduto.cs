namespace Glorific.Application.Ports;

/// <summary>
/// Porta pro provedor de IA que escreve a descrição do produto a partir da foto e de exemplos
/// de outras peças já cadastradas. Hoje o adaptador é OpenAI (Infrastructure); trocar de
/// provedor não deve tocar em nenhum serviço da Application.
/// </summary>
public interface IGeradorDescricaoProduto
{
    Task<string> GerarAsync(DescricaoProdutoPedido pedido, CancellationToken ct = default);
}

/// <summary>
/// Tudo que o adaptador precisa pra montar o prompt. A Application monta este objeto (busca o
/// produto, a capa da galeria e as descrições de referência); o adaptador só conhece IA.
/// </summary>
public sealed record DescricaoProdutoPedido
{
    public required string NomeProduto { get; init; }

    public string? ComposicaoTecido { get; init; }

    /// <summary>Bytes crus da foto de capa — o adaptador é quem decide como codificar pro provedor.</summary>
    public required byte[] ImagemBytes { get; init; }

    public required string ImagemContentType { get; init; }

    /// <summary>Descrições de outras peças já publicadas, usadas como referência de estilo/tom.</summary>
    public IReadOnlyList<string> DescricoesExemplo { get; init; } = [];
}
