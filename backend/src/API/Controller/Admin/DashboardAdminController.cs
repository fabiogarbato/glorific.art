using Glorific.Application.DTO.Painel;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Tela inicial do painel.
///
/// PainelAdmin e a policy certa aqui — qualquer papel administrativo (admin, gerente, operador)
/// precisa ver a fila de envio travada e o alerta de estoque para trabalhar. Exigir GestaoCatalogo
/// deixaria o operador de expedicao entrando num painel em branco.
///
/// As datas vao em UTC, que e como tudo e gravado. Sem elas, o servico usa os ultimos trinta dias.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacao.PainelAdmin)]
[Route("api/v1/admin/dashboard")]
public class DashboardAdminController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardAdminController(IDashboardService dashboard)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
    }

    /// <summary>
    /// Faturamento e pedidos do periodo, ticket medio, pedidos por status, mais vendidos, estoque
    /// abaixo do minimo, fila de envio com problema e avaliacoes pendentes de moderacao.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResumoDto>> ObterResumo(
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        CancellationToken cancellationToken)
    {
        var resumo = await _dashboard.ObterResumoAsync(de, ate, cancellationToken);
        return Ok(resumo);
    }
}
