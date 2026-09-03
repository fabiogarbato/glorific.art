using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// CRUD de colecoes (drops) e a curadoria de quais pecas entram em cada uma.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/colecoes")]
public sealed class ColecoesAdminController
    : GenericController<Colecao, ColecaoCreateDto, ColecaoUpdateDto, ColecaoResponseDto>
{
    private readonly IColecaoService _colecoes;

    public ColecoesAdminController(IColecaoService colecoes) : base(colecoes)
    {
        _colecoes = colecoes;
    }

    protected override int GetId(ColecaoResponseDto dto) => dto.Id;

    /// <summary>
    /// Vincula um produto a colecao. Revincular so muda a ordem — a curadoria da vitrine do drop
    /// e manual, e reenviar o mesmo produto nao pode duplicar o vinculo.
    /// </summary>
    [HttpPost("{id:int}/produtos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VincularProduto(
        int id,
        [FromBody] VincularProdutoColecaoDto dto,
        CancellationToken cancellationToken)
    {
        await _colecoes.VincularProdutoAsync(id, dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}/produtos/{idProduto:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesvincularProduto(
        int id,
        int idProduto,
        CancellationToken cancellationToken)
    {
        await _colecoes.DesvincularProdutoAsync(id, idProduto, cancellationToken);
        return NoContent();
    }
}
