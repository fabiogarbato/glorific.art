using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>Cores e swatches. Hex cobre cor solida; a midia de swatch cobre estampa.</summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/cores")]
public sealed class CoresAdminController
    : GenericController<Cor, CorCreateDto, CorUpdateDto, CorResponseDto>
{
    private readonly ICorService _cores;

    public CoresAdminController(ICorService cores) : base(cores)
    {
        _cores = cores;
    }

    protected override int GetId(CorResponseDto dto) => dto.Id;

    [HttpGet("ativas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CorResponseDto>>> Ativas(CancellationToken cancellationToken) =>
        Ok(await _cores.ObterAtivasOrdenadasAsync(cancellationToken));
}
