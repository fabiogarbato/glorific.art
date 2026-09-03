using Glorific.Api.Common;
using Glorific.Application.Common;
using Glorific.Application.DTO.Social;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Moderacao de avaliacoes.
///
/// Toda avaliacao nasce Pendente e nenhuma chega a vitrine sem passar por aqui. A decisao e de
/// risco de marca, nao de produto: comentario aberto em loja crista custa caro para despublicar
/// depois de circular.
///
/// Aprovar e rejeitar registram QUEM moderou e QUANDO. Sem isso, "por que essa review sumiu?" nao
/// tem resposta — e a pergunta sempre chega.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/avaliacoes")]
public class AvaliacoesAdminController : ControllerBase
{
    private readonly IAvaliacaoService _avaliacoes;
    private readonly IIdentidadeUsuarioService _identidade;

    public AvaliacoesAdminController(IAvaliacaoService avaliacoes, IIdentidadeUsuarioService identidade)
    {
        _avaliacoes = avaliacoes ?? throw new ArgumentNullException(nameof(avaliacoes));
        _identidade = identidade ?? throw new ArgumentNullException(nameof(identidade));
    }

    /// <summary>
    /// Fila de moderacao. Sem status na query, devolve as pendentes, da mais antiga para a mais
    /// nova — quem esta esperando ha mais tempo aparece primeiro.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AvaliacaoAdminResponseDto>>> Listar(
        [FromQuery] StatusAvaliacao? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var resultado = await _avaliacoes.ListarParaModeracaoAsync(
            status, new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    /// <summary>Publica a avaliacao e recalcula NotaMedia e TotalAvaliacoes do produto.</summary>
    [HttpPost("{id:int}/aprovar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvaliacaoAdminResponseDto>> Aprovar(
        int id,
        CancellationToken cancellationToken)
    {
        var idModerador = await ModeradorAtualAsync(cancellationToken);

        return Ok(await _avaliacoes.AprovarAsync(id, idModerador, cancellationToken));
    }

    /// <summary>
    /// Rejeita com motivo obrigatorio. Tambem recalcula as notas do produto: a rejeicao pode estar
    /// derrubando uma avaliacao que ja estava publicada e contando para a media.
    /// </summary>
    [HttpPost("{id:int}/rejeitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvaliacaoAdminResponseDto>> Rejeitar(
        int id,
        [FromBody] AvaliacaoRejeicaoDto dto,
        CancellationToken cancellationToken)
    {
        var idModerador = await ModeradorAtualAsync(cancellationToken);

        return Ok(await _avaliacoes.RejeitarAsync(id, idModerador, dto.Motivo, cancellationToken));
    }

    private Task<int> ModeradorAtualAsync(CancellationToken cancellationToken) =>
        _identidade.ObterIdPorUuidAsync(User.ObterUuid(), cancellationToken);
}
