namespace Glorific.Application.Models.MelhorEnvio;

/// <summary>
/// Resposta de POST {ME}/api/cart/checkout — passo 2 (NoCarrinho -> Comprado).
/// Esta e a chamada que CONSOME SALDO da carteira do Melhor Envio: saldo insuficiente volta como
/// 4xx do ME com o corpo dentro do detail, e o worker precisa entrar em backoff, nao desistir.
/// </summary>
public sealed record CompraEtiquetaResultado
{
    public required bool Sucesso { get; init; }

    /// <summary>purchase.id.</summary>
    public string? IdCompra { get; init; }

    public string? Protocolo { get; init; }

    /// <summary>purchase.status — "paid" no caminho feliz.</summary>
    public string? Status { get; init; }

    /// <summary>purchase.total em centavos.</summary>
    public int? TotalCentavos { get; init; }

    /// <summary>
    /// Custo real por etiqueta (meOrderId -> centavos). E o que vai em envios.valor_comprado;
    /// se o ME nao detalhar, o servico cai no ValorCotadoCentavos como fallback.
    /// </summary>
    public IReadOnlyDictionary<string, int> ValoresPorEtiqueta { get; init; } =
        new Dictionary<string, int>();

    public string? Mensagem { get; init; }

    public string? RawJson { get; init; }
}

/// <summary>
/// Resposta de POST {ME}/api/labels/generate — passo 3 (Comprado -> EtiquetaGerada).
/// O ME devolve um mapa id -> { status, message }; o "status" dele e booleano, nao texto.
/// </summary>
public sealed record GeracaoEtiquetaResultado
{
    public IReadOnlyDictionary<string, GeracaoEtiquetaItem> Itens { get; init; } =
        new Dictionary<string, GeracaoEtiquetaItem>();

    public string? RawJson { get; init; }

    /// <summary>Atalho para o caso de uma etiqueta so, que e o do worker.</summary>
    public bool Gerada(string meOrderId) =>
        Itens.TryGetValue(meOrderId, out var item) && item.Sucesso;
}

public sealed record GeracaoEtiquetaItem
{
    public bool Sucesso { get; init; }

    public string? Mensagem { get; init; }
}

/// <summary>
/// Modo do link de impressao (POST {ME}/api/labels/print).
/// Privado e o padrao do worker; Publico so sob demanda no botao do admin, porque gera um link
/// que qualquer pessoa com a URL abre.
/// </summary>
public enum ModoImpressaoEtiqueta
{
    Privado = 1,
    Publico = 2
}

/// <summary>
/// Resposta de POST {ME}/api/labels/print — passo 4.
/// Falha aqui NAO regride o status do envio: a URL e buscada sob demanda depois.
/// Vale tambem para GET /api/labels/{id}/file/{pdf|zpl|jpeg}, que devolve LINK e nao binario.
/// </summary>
public sealed record ImpressaoEtiquetaResultado
{
    public required string Url { get; init; }

    public string? RawJson { get; init; }
}
