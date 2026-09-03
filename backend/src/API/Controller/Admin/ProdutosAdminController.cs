using Glorific.Api.Configuration;
using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Painel de produtos: CRUD, grade de variacoes, galeria e auditoria.
///
/// Nao herda de GenericController de proposito. A listagem administrativa precisa de filtros
/// que o CRUD generico nao tem (ativo, categoria, busca) e o DELETE aqui e SOFT — herdar
/// exporia cinco actions genericas com semantica diferente da que este recurso tem.
///
/// As duas actions de variacao por id (PUT/DELETE /admin/variacoes/{id}) moram neste controller
/// com rota ABSOLUTA: sao operacoes sobre a variacao, mas conceitualmente parte da tela de
/// produto, e separa-las em um controller de duas actions so espalharia a mesma policy.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[ApiController]
[Route("api/v1/admin/produtos")]
[Produces("application/json")]
public sealed class ProdutosAdminController : ControllerBase
{
    private readonly IProdutoService _produtos;
    private readonly IProdutoVariacaoService _variacoes;
    private readonly IMidiaService _midias;

    public ProdutosAdminController(
        IProdutoService produtos,
        IProdutoVariacaoService variacoes,
        IMidiaService midias)
    {
        _produtos = produtos;
        _variacoes = variacoes;
        _midias = midias;
    }

    // ------------------------------------------------------------------
    // CRUD
    // ------------------------------------------------------------------

    /// <summary>Listagem do painel. ativo = null traz publicados e despublicados juntos.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProdutoResponseDto>>> Listar(
        [FromQuery] bool? ativo,
        [FromQuery] int? categoria,
        [FromQuery] string? q,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _produtos.ListarAdminAsync(
            new PageRequest(page, pageSize), ativo ?? true, categoria, q, cancellationToken));

    /// <summary>
    /// Produtos despublicados. Existe como rota propria porque e uma TELA do painel — e a
    /// resposta para "onde foi parar aquela peca que eu tirei do ar".
    /// </summary>
    [HttpGet("inativos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProdutoResponseDto>>> Inativos(
        [FromQuery] int? categoria,
        [FromQuery] string? q,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _produtos.ListarAdminAsync(
            new PageRequest(page, pageSize), ativo: false, categoria, q, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponseDto>> ObterPorId(int id, CancellationToken cancellationToken) =>
        Ok(await _produtos.ObterDetalheAdminAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProdutoResponseDto>> Criar(
        [FromBody] ProdutoCreateDto dto,
        CancellationToken cancellationToken)
    {
        var criado = await _produtos.CriarAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>O id vem da ROTA. O corpo nao carrega id — nao pode contradizer a URL.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponseDto>> Atualizar(
        int id,
        [FromBody] ProdutoUpdateDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _produtos.AtualizarAsync(id, dto, cancellationToken));

    /// <summary>
    /// SOFT delete: Ativo = false mais uma linha em logs_produtos. O produto continua existindo
    /// porque o historico de pedidos aponta para ele.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponseDto>> Desativar(int id, CancellationToken cancellationToken) =>
        Ok(await _produtos.DesativarAsync(id, UuidUsuarioAtual(), cancellationToken));

    [HttpPost("{id:int}/ativar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponseDto>> Ativar(int id, CancellationToken cancellationToken) =>
        Ok(await _produtos.AtivarAsync(id, UuidUsuarioAtual(), cancellationToken));

    /// <summary>Quem tirou do ar e quando.</summary>
    [HttpGet("{id:int}/logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProdutoLogResponseDto>>> Logs(
        int id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _produtos.ObterLogsAsync(id, new PageRequest(page, pageSize), cancellationToken));

    // ------------------------------------------------------------------
    // Variacoes (a grade)
    // ------------------------------------------------------------------

    [HttpGet("{id:int}/variacoes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProdutoVariacaoResponseDto>>> Variacoes(
        int id,
        [FromQuery] bool incluirInativas,
        CancellationToken cancellationToken) =>
        Ok(await _variacoes.ObterPorProdutoAsync(id, incluirInativas, cancellationToken));

    [HttpPost("{id:int}/variacoes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoVariacaoResponseDto>> CriarVariacao(
        int id,
        [FromBody] ProdutoVariacaoCreateDto dto,
        CancellationToken cancellationToken)
    {
        // O produto vem da ROTA e sobrescreve o que veio no corpo: sem isso um payload
        // malicioso criaria SKU dentro de outro produto.
        var criada = await _variacoes.CriarAsync(dto with { IdProduto = id }, cancellationToken);

        return CreatedAtAction(nameof(Variacoes), new { id }, criada);
    }

    /// <summary>
    /// Gera a matriz tamanhos x cores em lote. E o que torna o cadastro de moda viavel: 5
    /// tamanhos x 4 cores sao 20 SKUs, e cadastrar um a um faz o admin desistir.
    /// As combinacoes que ja existem sao preservadas como estao.
    /// </summary>
    [HttpPost("{id:int}/variacoes/gerar-grade")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradeGeradaDto>> GerarGrade(
        int id,
        [FromBody] GerarGradeDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _variacoes.GerarGradeAsync(id, dto, cancellationToken));

    [HttpPut("/api/v1/admin/variacoes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoVariacaoResponseDto>> AtualizarVariacao(
        int id,
        [FromBody] ProdutoVariacaoUpdateDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _variacoes.AtualizarAsync(id, dto, cancellationToken));

    /// <summary>Soft delete tambem aqui: o SKU aparece em pedido e etiqueta ja emitidos.</summary>
    [HttpDelete("/api/v1/admin/variacoes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesativarVariacao(int id, CancellationToken cancellationToken)
    {
        await _variacoes.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("/api/v1/admin/variacoes/{id:int}/ativar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoVariacaoResponseDto>> AtivarVariacao(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _variacoes.AtivarAsync(id, cancellationToken));

    // ------------------------------------------------------------------
    // Galeria
    // ------------------------------------------------------------------

    [HttpGet("{id:int}/midias")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MidiaProdutoResponseDto>>> Galeria(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _midias.ObterGaleriaAsync(id, cancellationToken));

    [HttpPost("{id:int}/midias")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MidiaProdutoResponseDto>> VincularMidia(
        int id,
        [FromBody] VincularMidiaProdutoDto dto,
        CancellationToken cancellationToken)
    {
        var vinculo = await _midias.VincularAoProdutoAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(Galeria), new { id }, vinculo);
    }

    /// <summary>A primeira posicao da lista vira a capa — ordem explicita, nunca por menor id.</summary>
    [HttpPut("{id:int}/midias/ordem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MidiaProdutoResponseDto>>> ReordenarGaleria(
        int id,
        [FromBody] ReordenarGaleriaDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _midias.ReordenarGaleriaAsync(id, dto, cancellationToken));

    [HttpDelete("{id:int}/midias/{idMidia:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesvincularMidia(int id, int idMidia, CancellationToken cancellationToken)
    {
        await _midias.DesvincularDoProdutoAsync(id, idMidia, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Uuid do autor da acao, vindo da claim "sub" do proprio token — nunca do corpo da
    /// requisicao, senao a auditoria registraria quem o cliente disser que e.
    /// </summary>
    private string? UuidUsuarioAtual() => User.FindFirst(AutenticacaoConfiguration.ClaimSub)?.Value;
}
