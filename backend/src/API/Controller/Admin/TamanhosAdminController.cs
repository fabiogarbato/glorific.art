using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>Grade de tamanhos. A coluna Ordem e o que faz o seletor sair PP, P, M, G, GG.</summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/tamanhos")]
public sealed class TamanhosAdminController
    : GenericController<Tamanho, TamanhoCreateDto, TamanhoUpdateDto, TamanhoResponseDto>
{
    private readonly ITamanhoService _tamanhos;

    public TamanhosAdminController(ITamanhoService tamanhos) : base(tamanhos)
    {
        _tamanhos = tamanhos;
    }

    protected override int GetId(TamanhoResponseDto dto) => dto.Id;

    [HttpGet("ativos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TamanhoResponseDto>>> Ativos(
        [FromQuery] GradeTamanho? grade,
        CancellationToken cancellationToken) =>
        Ok(await _tamanhos.ObterAtivosOrdenadosAsync(grade, cancellationToken));
}
