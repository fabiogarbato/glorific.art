using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Guias de medidas. O CRUD generico ja resolve tudo: o servico substitui as linhas em bloco a
/// cada PUT, e o detalhe sempre volta com as linhas na ordem da grade.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/tabelas-medidas")]
public sealed class TabelasMedidasAdminController
    : GenericController<TabelaMedidas, TabelaMedidasCreateDto, TabelaMedidasUpdateDto, TabelaMedidasResponseDto>
{
    public TabelasMedidasAdminController(ITabelaMedidasService tabelas) : base(tabelas)
    {
    }

    protected override int GetId(TabelaMedidasResponseDto dto) => dto.Id;
}
