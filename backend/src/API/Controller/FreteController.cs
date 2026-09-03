using Glorific.Api.Configuration;
using Glorific.Application.DTO.Frete;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Glorific.Api.Controller;

/// <summary>
/// Cotacao de frete avulsa — a da pagina de produto, onde o cliente digita o CEP antes de ter
/// carrinho. A cotacao do carrinho ja montado fica em POST /api/v1/carrinho/frete.
///
/// PUBLICO E COM RATE LIMIT, e as duas coisas sao decisao consciente:
/// - publico porque exigir login para simular frete e o jeito mais rapido de perder a venda;
/// - com limite porque cada chamada vira uma consulta paga no Melhor Envio que leva de 2 a 5 s.
///   Um bot cotando em laco nao derruba a loja, mas queima a cota da conta e prende workers de
///   requisicao esperando o parceiro.
///
/// O corpo traz apenas id de variacao e quantidade. Peso, dimensao e valor declarado saem de
/// produto_variacoes: aceitar peso do navegador seria aceitar frete forjado.
/// </summary>
[ApiController]
[Route("api/v1/frete")]
[AllowAnonymous]
[EnableRateLimiting(PoliticasRateLimit.Frete)]
[Produces("application/json")]
public sealed class FreteController : ControllerBase
{
    private readonly IFreteService _fretes;

    public FreteController(IFreteService fretes)
    {
        _fretes = fretes ?? throw new ArgumentNullException(nameof(fretes));
    }

    /// <summary>
    /// Opcoes de frete para os itens informados, ja ordenadas por preco, com o prazo somado ao
    /// manuseio da loja e com a regra de frete gratis aplicada.
    /// </summary>
    [HttpPost("cotacao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<OpcaoFreteResponseDto>>> Cotar(
        [FromBody] CotacaoFreteRequestDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _fretes.CotarAsync(dto, cancellationToken));
    }
}
