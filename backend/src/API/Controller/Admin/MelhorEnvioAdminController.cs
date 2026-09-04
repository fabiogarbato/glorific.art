using Glorific.Application.DTO.MelhorEnvio;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Fluxo OAuth de conexao com o Melhor Envio: comeca a autorizacao, recebe o retorno e expoe o
/// status atual da conta.
///
/// O "code" do OAuth volta para a RAIZ do site (https://hml.glorific.art/ — endereco fixo,
/// cadastrado no app do Melhor Envio), nao para uma rota de API. Por isso o SPA e quem detecta
/// "?code=...&amp;state=..." na URL na Home e chama /conectar aqui, autenticado como admin.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.SomenteAdmin)]
[ApiController]
[Route("api/v1/admin/melhor-envio")]
[Produces("application/json")]
public sealed class MelhorEnvioAdminController : ControllerBase
{
    private const string PrefixoCacheState = "me_oauth_state:";
    private static readonly TimeSpan ValidadeState = TimeSpan.FromMinutes(10);

    private readonly IMelhorEnvioClient _client;
    private readonly IMemoryCache _cache;

    public MelhorEnvioAdminController(IMelhorEnvioClient client, IMemoryCache cache)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Redireciona para a tela de autorizacao do Melhor Envio. O "state" e um valor aleatorio
    /// guardado em cache por 10 min — o retorno so e aceito se trouxer o MESMO state, protecao
    /// contra alguem forjar um "code" de outra sessao (CSRF do fluxo OAuth).
    /// </summary>
    [HttpGet("autorizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Autorizar()
    {
        // Devolve a URL em JSON (nao um 302 direto) de proposito: esta rota exige admin
        // autenticado por Bearer token, e uma navegacao pura do navegador (window.location) nao
        // manda esse header. O SPA busca a URL autenticado e SO ENTAO navega de verdade.
        var state = Guid.NewGuid().ToString("N");
        _cache.Set(PrefixoCacheState + state, true, ValidadeState);

        return Ok(new { url = _client.ObterUrlAutorizacao(state) });
    }

    /// <summary>
    /// Completa a conexao: confere o state, troca o code por token e persiste. Chamado pelo SPA
    /// assim que detecta "?code=...&amp;state=..." na Home (o endereco de retorno cadastrado no
    /// Melhor Envio).
    /// </summary>
    [HttpPost("conectar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Conectar(
        [FromBody] ConectarMelhorEnvioDto dto,
        CancellationToken cancellationToken)
    {
        var chaveState = PrefixoCacheState + dto.State;

        if (string.IsNullOrWhiteSpace(dto.State) || !_cache.TryGetValue(chaveState, out _))
        {
            return BadRequest(new
            {
                error = "Autorização expirada ou já usada. Clique em conectar de novo.",
            });
        }

        _cache.Remove(chaveState);

        try
        {
            await _client.ConectarAsync(dto.Code, cancellationToken);
        }
        catch (MelhorEnvioApiException excecao)
        {
            return BadRequest(new { error = excecao.DetalheAmigavel });
        }

        return NoContent();
    }

    /// <summary>Status atual da conta — conectada ou não, e se o token está perto de expirar.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await _client.VerificarStatusContaAsync(cancellationToken);
        return Ok(status);
    }
}
