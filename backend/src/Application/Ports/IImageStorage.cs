using Glorific.Application.Models.Midia;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de armazenamento de imagem (Cloudinary hoje).
///
/// Recebe Stream, e nao byte[] nem IFormFile: byte[] carrega o arquivo inteiro na memoria do
/// servidor (uma sessao de fotos de produto sobe dezenas de arquivos de varios MB) e IFormFile
/// e tipo de ASP.NET — a Application nao pode conhece-lo.
/// </summary>
public interface IImageStorage
{
    /// <summary>
    /// Sobe a imagem e devolve a URL publica, o PublicId (necessario para remover depois) e as
    /// dimensoes finais.
    /// </summary>
    /// <param name="conteudo">Stream posicionado no inicio. Quem abriu e quem fecha.</param>
    /// <param name="nomeArquivo">Nome original, usado apenas para derivar extensao e slug.</param>
    /// <param name="contentType">MIME conferido pelo servico ANTES de chegar aqui.</param>
    Task<ImagemArmazenadaInfo> EnviarAsync(
        Stream conteudo,
        string nomeArquivo,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Remove pelo PublicId.
    ///
    /// Idempotente: remover algo que ja nao existe nao e erro. Isso importa porque a remocao roda
    /// DEPOIS do commit que apagou a linha em midias — se lancasse, uma reexecucao da limpeza
    /// deixaria o processo travado num arquivo fantasma.
    /// </summary>
    Task RemoverAsync(string publicId, CancellationToken ct = default);
}
