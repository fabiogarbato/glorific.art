using Glorific.Application.DTO.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface IMidiaService
    : IGenericService<Glorific.Domain.Entities.Catalogo.Midia, MidiaCreateDto, MidiaUpdateDto, MidiaResponseDto>
{
    /// <summary>
    /// Sobe a imagem pelo IImageStorage e registra a linha em midias.
    ///
    /// Recebe Stream e nao IFormFile: a Application nao pode conhecer tipo de ASP.NET, e byte[]
    /// carregaria o arquivo inteiro na memoria do servidor — uma sessao de fotos de produto sobe
    /// dezenas de arquivos de varios MB.
    /// </summary>
    Task<MidiaResponseDto> EnviarAsync(
        Stream conteudo,
        string nomeArquivo,
        string contentType,
        long tamanhoBytes,
        string? altText = null,
        CancellationToken cancellationToken = default);

    /// <summary>Galeria do produto: capa primeiro, depois a ordem explicita.</summary>
    Task<IReadOnlyList<MidiaProdutoResponseDto>> ObterGaleriaAsync(
        int idProduto,
        CancellationToken cancellationToken = default);

    Task<MidiaProdutoResponseDto> VincularAoProdutoAsync(
        int idProduto,
        VincularMidiaProdutoDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>A primeira posicao da lista vira a capa.</summary>
    Task<IReadOnlyList<MidiaProdutoResponseDto>> ReordenarGaleriaAsync(
        int idProduto,
        ReordenarGaleriaDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Desvincula a midia do produto. O arquivo no storage NAO e removido aqui: a mesma midia
    /// pode estar em outro produto, e a varredura de orfas e quem apaga de verdade.
    /// </summary>
    Task DesvincularDoProdutoAsync(
        int idProduto,
        int idMidia,
        CancellationToken cancellationToken = default);
}
