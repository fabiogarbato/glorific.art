using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// CRUD de categorias do painel. Policy na CLASSE: catalogo e preco sao de admin e gerente.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/categorias")]
public sealed class CategoriasAdminController
    : GenericController<Categoria, CategoriaCreateDto, CategoriaUpdateDto, CategoriaResponseDto>
{
    private readonly ICategoriaService _categorias;

    public CategoriasAdminController(ICategoriaService categorias) : base(categorias)
    {
        _categorias = categorias;
    }

    protected override int GetId(CategoriaResponseDto dto) => dto.Id;

    /// <summary>Arvore completa, incluindo as desabilitadas: o painel edita o que a loja esconde.</summary>
    [HttpGet("arvore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoriaResponseDto>>> Arvore(
        [FromQuery] bool somenteHabilitadas,
        CancellationToken cancellationToken) =>
        Ok(await _categorias.ObterArvoreAsync(somenteHabilitadas, cancellationToken));
}
