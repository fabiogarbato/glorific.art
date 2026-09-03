using System.Security.Claims;
using Glorific.Api.Configuration;
using Glorific.Application.Common;
using Glorific.Application.DTO.Pedidos;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Pedidos do proprio cliente.
///
/// Todas as rotas identificam o pedido por Uuid, nunca pelo Id inteiro: id sequencial em URL e
/// convite a enumeracao. E o servico filtra por usuario DENTRO da consulta, entao pedido de outra
/// pessoa responde 404 — 403 confirmaria que ele existe.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/pedidos")]
public sealed class PedidosController : ControllerBase
{
    private readonly IPedidoService _pedidos;

    public PedidosController(IPedidoService pedidos)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
    }

    /// <summary>Meus pedidos, do mais recente para o mais antigo. Sempre paginado.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<PedidoResumoResponseDto>>> Listar(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // PageRequest normaliza no construtor: page=0 vira 1 e pageSize=999999 vira o teto.
        var resultado = await _pedidos.ListarMeusAsync(
            UsuarioUuid(), new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    /// <summary>Recibo do pedido: tudo o que aparece aqui e snapshot congelado na compra.</summary>
    [HttpGet("{uuid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> ObterPorUuid(
        string uuid,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.ObterMeuAsync(UsuarioUuid(), uuid, cancellationToken);

        return Ok(pedido);
    }

    /// <summary>
    /// Timeline de rastreio. Le o historico ja gravado em envios_eventos — nao chama a
    /// transportadora a cada request: quem sincroniza com o Melhor Envio e o worker.
    /// </summary>
    [HttpGet("{uuid}/rastreio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RastreioResponseDto>> ObterRastreio(
        string uuid,
        CancellationToken cancellationToken)
    {
        var rastreio = await _pedidos.ObterRastreioAsync(UsuarioUuid(), uuid, cancellationToken);

        return Ok(rastreio);
    }

    private string UsuarioUuid() =>
        User.FindFirstValue(AutenticacaoConfiguration.ClaimSub)
        ?? throw new UnauthorizedAccessException("Token sem a claim de identidade.");
}
