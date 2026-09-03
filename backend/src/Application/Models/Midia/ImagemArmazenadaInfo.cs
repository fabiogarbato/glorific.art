namespace Glorific.Application.Models.Midia;

/// <summary>
/// Retorno do armazenamento de imagem (Cloudinary hoje, S3/R2 amanha).
///
/// Largura e Altura vem do provedor porque o front precisa da proporcao para reservar o espaco
/// da foto ANTES do download — em vitrine de moda a imagem e o produto, e layout pulando
/// enquanto carrega custa conversao.
///
/// PublicId e o identificador de remocao. Guardar em midias: sem ele, apagar o produto deixa o
/// arquivo pago no provedor para sempre.
/// </summary>
public sealed record ImagemArmazenadaInfo
{
    /// <summary>URL publica definitiva (https).</summary>
    public required string Url { get; init; }

    /// <summary>Identificador no provedor, usado por RemoverAsync.</summary>
    public required string PublicId { get; init; }

    public int Largura { get; init; }

    public int Altura { get; init; }

    /// <summary>Bytes do arquivo final, quando o provedor informa.</summary>
    public long? TamanhoBytes { get; init; }

    public string? Formato { get; init; }
}
