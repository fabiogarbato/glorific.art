using System.Security.Claims;
using Glorific.Api.Configuration;
using Glorific.Application.Common;
using Glorific.Application.DTO.Pedidos;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Expedicao: fila de pedidos, mudanca de status, etiqueta, rastreio e cancelamento.
///
/// A policy Expedicao (admin, gerente ou operador) fica na CLASSE: e a autorizacao mais
/// permissiva desta area e nenhuma action pode ser mais frouxa que ela. O operador de expedicao
/// enxerga pedido e envio, e nada de catalogo, preco ou usuario.
///
/// Todas as rotas identificam o pedido por Uuid. O Id inteiro nao atravessa a fronteira HTTP nem
/// aqui: a ponte para o identificador interno mora no servico.
/// </summary>
[ApiController]
[Authorize(Policy = PoliticasAutorizacao.Expedicao)]
[Produces("application/json")]
[Route("api/v1/admin/pedidos")]
public sealed class PedidosAdminController : ControllerBase
{
    private readonly IPedidoService _pedidos;

    public PedidosAdminController(IPedidoService pedidos)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
    }

    /// <summary>Fila de trabalho, com filtro por status, texto e periodo. Sempre paginada.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<PedidoResumoResponseDto>>> Listar(
        [FromQuery] string? status,
        [FromQuery] string? busca,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // Os filtros chegam como parametros soltos, e nao como um objeto complexo em [FromQuery]:
        // e o padrao que os demais controllers do projeto ja usam, e mantem a query string do
        // painel legivel (?status=Pago&busca=GA-2026) em vez de aninhada.
        var filtro = new PedidoFiltroAdminDto
        {
            Status = status,
            Busca = busca,
            De = de,
            Ate = ate
        };

        var resultado = await _pedidos.ListarAdminAsync(
            filtro, new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    /// <summary>Detalhe operacional. Difere do detalhe do cliente por incluir a URL da etiqueta.</summary>
    [HttpGet("{uuid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> ObterPorUuid(
        string uuid,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.ObterAdminAsync(uuid, cancellationToken);

        return Ok(pedido);
    }

    /// <summary>
    /// Mudanca manual de status. PATCH e nao PUT: e alteracao parcial de um campo, e um PUT
    /// sugeriria substituir o pedido inteiro pelo corpo enviado.
    /// </summary>
    [HttpPatch("{uuid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> AlterarStatus(
        string uuid,
        [FromBody] AlterarStatusPedidoDto dto,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.AlterarStatusAsync(uuid, dto, UsuarioUuid(), cancellationToken);

        return Ok(pedido);
    }

    /// <summary>
    /// Cancela o pedido: devolve estoque conforme o estagio e cancela a etiqueta no parceiro.
    /// Nao e DELETE porque nada e apagado — pedido cancelado continua existindo e auditavel.
    /// </summary>
    [HttpPost("{uuid}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> Cancelar(
        string uuid,
        [FromBody] CancelarPedidoDto dto,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.CancelarAsync(uuid, dto, UsuarioUuid(), cancellationToken);

        return Ok(pedido);
    }

    /// <summary>
    /// Empurra a etiqueta manualmente, sem esperar o ciclo de 60 s do worker.
    ///
    /// Nao ha risco de etiqueta duplicada: o servico reivindica a linha com o MESMO claim atomico
    /// que o worker usa, e quem perde a corrida simplesmente nao faz nada.
    /// </summary>
    [HttpPost("{uuid}/etiqueta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> GerarEtiqueta(
        string uuid,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.GerarEtiquetaAsync(uuid, cancellationToken);

        return Ok(pedido);
    }

    /// <summary>
    /// URL do PDF da etiqueta.
    ///
    /// publico = true gera um link ABERTO, que qualquer pessoa com a URL consegue abrir. Por isso
    /// e opt-in explicito e vive so aqui, atras da policy de expedicao — nunca no detalhe do cliente.
    /// </summary>
    [HttpGet("{uuid}/etiqueta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterEtiqueta(
        string uuid,
        [FromQuery] bool publico,
        CancellationToken cancellationToken)
    {
        var url = await _pedidos.ObterUrlEtiquetaAsync(uuid, publico, cancellationToken);

        if (string.IsNullOrWhiteSpace(url))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Etiqueta indisponivel",
                detail: "A etiqueta ainda nao foi gerada para este pedido.");
        }

        return Ok(new { url });
    }

    /// <summary>
    /// Sincroniza o rastreio sob demanda, para o atendimento nao precisar esperar o proximo ciclo
    /// do worker enquanto o cliente esta ao telefone.
    /// </summary>
    [HttpPost("{uuid}/rastreio/sincronizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoResponseDto>> SincronizarRastreio(
        string uuid,
        CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.SincronizarRastreioAsync(uuid, cancellationToken);

        return Ok(pedido);
    }

    private string UsuarioUuid() =>
        User.FindFirstValue(AutenticacaoConfiguration.ClaimSub)
        ?? throw new UnauthorizedAccessException("Token sem a claim de identidade.");
}
