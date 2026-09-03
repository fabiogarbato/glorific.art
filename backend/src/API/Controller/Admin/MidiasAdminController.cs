using Glorific.Api.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Acervo de imagens do catalogo.
///
/// O upload e um endpoint proprio, multipart, e nao o POST do CRUD generico: o corpo e um
/// arquivo, nao um JSON. O controller so faz a ponte — abre o stream do IFormFile e entrega ao
/// servico. Toda a validacao de formato e tamanho mora na Application, porque IFormFile e tipo
/// de ASP.NET e a Application nao pode conhece-lo.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/midias")]
public sealed class MidiasAdminController
    : GenericController<Midia, MidiaCreateDto, MidiaUpdateDto, MidiaResponseDto>
{
    /// <summary>
    /// Teto de corpo da requisicao, um pouco acima do limite de negocio (8 MB) para que um
    /// arquivo levemente maior chegue ao servico e receba a mensagem de erro certa, em vez de
    /// ser cortado pelo servidor com um 413 sem explicacao.
    /// </summary>
    private const long TamanhoMaximoRequisicao = 12L * 1024 * 1024;

    private readonly IMidiaService _midias;

    public MidiasAdminController(IMidiaService midias) : base(midias)
    {
        _midias = midias;
    }

    protected override int GetId(MidiaResponseDto dto) => dto.Id;

    [HttpPost("upload")]
    [RequestSizeLimit(TamanhoMaximoRequisicao)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MidiaResponseDto>> Enviar(
        IFormFile arquivo,
        [FromForm] string? altText,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(RespostaErro.Criar(
                HttpContext, StatusCodes.Status400BadRequest, "Selecione um arquivo de imagem."));

        // O stream do IFormFile e descartavel e pertence a requisicao: quem abre e quem fecha.
        await using var conteudo = arquivo.OpenReadStream();

        var midia = await _midias.EnviarAsync(
            conteudo,
            arquivo.FileName,
            arquivo.ContentType,
            arquivo.Length,
            altText,
            cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = midia.Id }, midia);
    }
}
