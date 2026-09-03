using System.Security.Claims;
using Glorific.Api.Configuration;
using Glorific.Application.DTO.Checkout;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Fechamento de compra.
///
/// [Authorize] EXPLICITO mesmo com a FallbackPolicy ja exigindo autenticacao: quem le este
/// arquivo tem de enxergar de imediato que aqui nao entra visitante anonimo.
///
/// Nao herda de GenericController porque checkout nao e CRUD: nao existe listar, atualizar nem
/// remover um checkout.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/checkout")]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkout;

    public CheckoutController(ICheckoutService checkout)
    {
        _checkout = checkout ?? throw new ArgumentNullException(nameof(checkout));
    }

    /// <summary>
    /// Fecha o pedido e devolve a URL de pagamento.
    ///
    /// O corpo NAO carrega usuario, preco, frete nem total — so a escolha de endereco e servico.
    /// Tudo o que vira dinheiro e recalculado no servidor.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckoutCriadoResponseDto>> Finalizar(
        [FromBody] CheckoutRequestDto requisicao,
        CancellationToken cancellationToken)
    {
        var criado = await _checkout.FinalizarAsync(UsuarioUuid(), requisicao, cancellationToken);

        // 201 com Location apontando para o status: e o recurso que o front vai consultar em
        // seguida enquanto o cliente paga.
        return CreatedAtAction(nameof(ConsultarStatus), new { uuid = criado.Uuid }, criado);
    }

    /// <summary>
    /// Alvo do polling da tela de aguardo. Le o estado local; a conferencia no gateway acontece
    /// no fluxo de webhook e de retorno, nao aqui — senao o navegador do cliente viraria um
    /// gerador gratuito de chamadas ao provedor.
    /// </summary>
    [HttpGet("{uuid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckoutStatusResponseDto>> ConsultarStatus(
        string uuid,
        CancellationToken cancellationToken)
    {
        var status = await _checkout.ConsultarStatusAsync(UsuarioUuid(), uuid, cancellationToken);

        return Ok(status);
    }

    /// <summary>
    /// Identidade vem SEMPRE do token, nunca do corpo. O claim curto e "sub" porque o handler do
    /// JwtBearer roda com MapInboundClaims desligado.
    /// </summary>
    private string UsuarioUuid() =>
        User.FindFirstValue(AutenticacaoConfiguration.ClaimSub)
        ?? throw new UnauthorizedAccessException("Token sem a claim de identidade.");
}
