using Glorific.Application.Common;
using Glorific.Application.DTO.Promocoes;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Promocoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// CRUD de cupom no painel.
///
/// Herda o GenericController pelo CRUD de verdade: cupom e um cadastro com listar, obter, criar,
/// alterar e excluir. A policy GestaoCatalogo na classe vale para todas as actions herdadas —
/// e onde admin e gerente mexem em preco e promocao, e o operador de expedicao nao.
///
/// Nao existe rota publica de cupom aqui. Validar codigo digitado pelo cliente e responsabilidade
/// do carrinho, que chama ICupomService.ValidarAsync — expor este controller ao publico daria um
/// oraculo para enumerar promocoes ativas.
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.GestaoCatalogo)]
[Route("api/v1/admin/cupons")]
public class CuponsAdminController
    : GenericController<Cupom, CupomCreateDto, CupomUpdateDto, CupomResponseDto>
{
    private readonly ICupomService _cupons;

    public CuponsAdminController(ICupomService cupons) : base(cupons)
    {
        _cupons = cupons ?? throw new ArgumentNullException(nameof(cupons));
    }

    /// <inheritdoc />
    protected override int GetId(CupomResponseDto dto) => dto.Id;

    /// <summary>
    /// Listagem com busca por codigo/descricao e filtro de ativo.
    ///
    /// Os dois filtros extras sao lidos de Request.Query e nao da assinatura porque a assinatura e
    /// a do metodo sobrescrito — o alternativo seria uma segunda rota de listagem, e duas rotas
    /// para a mesma tela e como o front do repo de referencia acabou com "/api/Categoria" e
    /// "/api/categorias" no mesmo arquivo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public override async Task<ActionResult<PagedResult<CupomResponseDto>>> Listar(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var busca = Request.Query["search"].ToString();

        bool? ativo = bool.TryParse(Request.Query["ativo"].ToString(), out var valor) ? valor : null;

        var resultado = await _cupons.ListarAdminAsync(
            string.IsNullOrWhiteSpace(busca) ? null : busca,
            ativo,
            new PageRequest(page, pageSize),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>Busca pelo codigo digitado, ja normalizado em maiusculas.</summary>
    [HttpGet("por-codigo/{codigo}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CupomResponseDto>> ObterPorCodigo(
        string codigo,
        CancellationToken cancellationToken)
    {
        var cupom = await _cupons.ObterPorCodigoAsync(codigo, cancellationToken);
        return Ok(cupom);
    }

    /// <summary>
    /// Ledger de usos: quem usou, em qual pedido e quanto foi descontado de fato. E o relatorio de
    /// quanto a campanha custou, e nao de quantas vezes o codigo foi digitado.
    /// </summary>
    [HttpGet("{id:int}/usos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CupomUsoResponseDto>>> ListarUsos(
        int id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var usos = await _cupons.ListarUsosAsync(id, new PageRequest(page, pageSize), cancellationToken);
        return Ok(usos);
    }
}
