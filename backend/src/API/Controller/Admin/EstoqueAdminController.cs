using System.Security.Claims;
using Glorific.Api.Configuration;
using Glorific.Application.Common;
using Glorific.Application.DTO.Estoque;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Estoque no painel: relatorio de minimo, extrato do ledger, entrada de nota e ajuste de
/// inventario.
///
/// Policy Expedicao (admin, gerente e operador) e nao GestaoCatalogo: quem conta prateleira e
/// recebe nota do fornecedor e a expedicao. Exigir gerente para lancar uma entrada faria o
/// operador pedir a senha de alguem — e a senha compartilhada e pior que a permissao ampla.
///
/// O usuario que assina cada movimentacao sai da claim sub do token, nunca do corpo: assinatura
/// de auditoria enviada pelo cliente nao e auditoria.
/// </summary>
[ApiController]
[Route("api/v1/admin/estoque")]
[Authorize(Policy = PoliticasAutorizacao.Expedicao)]
[Produces("application/json")]
public sealed class EstoqueAdminController : ControllerBase
{
    private readonly IEstoqueService _estoques;

    public EstoqueAdminController(IEstoqueService estoques)
    {
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
    }

    /// <summary>
    /// Relatorio de reposicao: SKUs com disponivel abaixo do minimo configurado.
    /// Sem paginacao de proposito — e uma lista de acao, curta por definicao. Se ela crescer
    /// demais, o problema e de compras, nao de paginacao.
    /// </summary>
    [HttpGet("alerta-minimo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EstoqueVariacaoResponseDto>>> AlertaMinimo(
        CancellationToken cancellationToken)
    {
        return Ok(await _estoques.ObterAbaixoDoMinimoAsync(cancellationToken));
    }

    /// <summary>Saldo de um SKU: fisico, reservado e disponivel.</summary>
    [HttpGet("variacao/{idVariacao:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstoqueVariacaoResponseDto>> ObterPorVariacao(
        int idVariacao,
        CancellationToken cancellationToken)
    {
        return Ok(await _estoques.ObterPorVariacaoAsync(idVariacao, cancellationToken));
    }

    /// <summary>Minimo de alerta e localizacao fisica. Nao mexe em saldo.</summary>
    [HttpPut("variacao/{idVariacao:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstoqueVariacaoResponseDto>> AtualizarParametros(
        int idVariacao,
        [FromBody] EstoqueParametrosUpdateDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _estoques.AtualizarParametrosAsync(idVariacao, dto, cancellationToken));
    }

    /// <summary>
    /// Entrada de estoque em lote (nota do fornecedor, producao, devolucao aprovada).
    /// A nota inteira entra numa transacao so: meia nota lancada e pior que nenhuma.
    /// </summary>
    [HttpPost("entrada")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EstoqueVariacaoResponseDto>>> RegistrarEntrada(
        [FromBody] EstoqueEntradaDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _estoques.RegistrarEntradaAsync(dto, UuidUsuario(), cancellationToken));
    }

    /// <summary>
    /// Ajuste de inventario pela contagem fisica encontrada.
    /// Reducao que invadiria estoque reservado e recusada: derrubaria pedido ja pago.
    /// </summary>
    [HttpPost("ajuste")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstoqueVariacaoResponseDto>> Ajustar(
        [FromBody] EstoqueAjusteDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _estoques.AjustarAsync(dto, UuidUsuario(), cancellationToken));
    }

    /// <summary>
    /// Extrato do ledger, paginado e filtravel. O ledger e append-only e cresce para sempre:
    /// nenhuma rota devolve ele inteiro.
    /// </summary>
    [HttpGet("movimentacoes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MovimentacaoEstoqueResponseDto>>> Movimentacoes(
        [FromQuery] int? idVariacao,
        [FromQuery] int? idPedido,
        [FromQuery] string? movimento,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var filtro = new MovimentacaoEstoqueFiltro
        {
            IdVariacao = idVariacao,
            IdPedido = idPedido,
            Movimento = movimento,

            // As datas do filtro sao interpretadas em UTC, que e como a coluna e gravada.
            // Sem o SpecifyKind, um "2026-09-01" vindo do painel viraria hora local do host e o
            // extrato perderia (ou duplicaria) as tres primeiras horas do dia.
            DeUtc = ParaUtc(de),
            AteUtc = ParaUtc(ate)
        };

        var resultado = await _estoques.ListarMovimentacoesAsync(
            filtro, new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    private static DateTime? ParaUtc(DateTime? data) =>
        data is null ? null : DateTime.SpecifyKind(data.Value, DateTimeKind.Utc);

    private string? UuidUsuario() => User.FindFirstValue(AutenticacaoConfiguration.ClaimSub);
}
