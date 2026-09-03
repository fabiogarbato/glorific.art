using Glorific.Api.Common;
using Glorific.Application.Common;
using Glorific.Application.DTO.Social;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Avaliacoes de produto.
///
/// Leitura publica, escrita autenticada — e por isso que o [Authorize] fica nas actions e nao na
/// classe. A FallbackPolicy do projeto exige usuario autenticado por omissao, entao cada rota
/// publica declara [AllowAnonymous] EXPLICITO: esquecer o atributo vira 401 barulhento em vez de
/// vazamento silencioso.
///
/// Nao herda GenericController porque avaliacao nao tem PUT nem DELETE: o ciclo de vida e
/// nasce pendente, vira aprovada ou rejeitada, e para. Expor as duas actions do CRUD generico
/// abriria caminho para reescrever texto ja moderado e para apagar linha de que a nota
/// denormalizada do produto depende.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/avaliacoes")]
public class AvaliacoesController : ControllerBase
{
    private readonly IAvaliacaoService _avaliacoes;
    private readonly IIdentidadeUsuarioService _identidade;

    public AvaliacoesController(IAvaliacaoService avaliacoes, IIdentidadeUsuarioService identidade)
    {
        _avaliacoes = avaliacoes ?? throw new ArgumentNullException(nameof(avaliacoes));
        _identidade = identidade ?? throw new ArgumentNullException(nameof(identidade));
    }

    /// <summary>Avaliacoes APROVADAS do produto, paginadas.</summary>
    [AllowAnonymous]
    [HttpGet("produtos/{idProduto:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AvaliacaoResponseDto>>> ListarDoProduto(
        int idProduto,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var resultado = await _avaliacoes.ListarDoProdutoAsync(
            idProduto, new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Media, distribuicao por nota, percentual de recomendacao e caimento predominante.
    /// E o bloco que faz a pagina de produto dizer "a maioria diz que veste pequeno".
    /// </summary>
    [AllowAnonymous]
    [HttpGet("produtos/{idProduto:int}/resumo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AvaliacaoResumoDto>> ObterResumo(
        int idProduto,
        CancellationToken cancellationToken)
    {
        var resumo = await _avaliacoes.ObterResumoDoProdutoAsync(idProduto, cancellationToken);
        return Ok(resumo);
    }

    /// <summary>
    /// Envia uma avaliacao. Entra como Pendente e so aparece na vitrine depois da moderacao —
    /// o 201 aqui significa "recebida", nao "publicada".
    /// </summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvaliacaoResponseDto>> Criar(
        [FromBody] AvaliacaoCreateDto dto,
        CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        var criada = await _avaliacoes.CriarAsync(idUsuario, dto, cancellationToken);

        return CreatedAtAction(
            nameof(ListarDoProduto),
            new { idProduto = criada.IdProduto },
            criada);
    }

    /// <summary>
    /// A claim sub carrega usuarios.Uuid; o Id inteiro nunca sai para o front. A traducao passa
    /// pelo servico de identidade, que tambem recusa token de conta desativada.
    /// </summary>
    private Task<int> UsuarioAtualAsync(CancellationToken cancellationToken) =>
        _identidade.ObterIdPorUuidAsync(User.ObterUuid(), cancellationToken);
}
