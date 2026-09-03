using Glorific.Application.DTO.Config;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Configuracao operacional da loja. Linha unica: nao ha listar, criar nem remover.
///
/// SomenteAdmin, e nao GestaoCatalogo: o que se muda aqui vale para a loja inteira e no ato —
/// CEP de origem quebra toda cotacao de frete, prazo de manuseio empurra todo prazo exibido,
/// pedido minimo barra checkout. Nao e decisao de catalogo, e decisao de operacao.
///
/// A leitura passa pelo cache em memoria do servico; o PUT invalida o cache no mesmo passo, para o
/// admin ver o efeito na proxima cotacao e nao daqui a dez minutos.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacao.SomenteAdmin)]
[Route("api/v1/admin/configuracoes")]
public class ConfiguracaoAdminController : ControllerBase
{
    private readonly IConfiguracaoLojaService _configuracoes;

    public ConfiguracaoAdminController(IConfiguracaoLojaService configuracoes)
    {
        _configuracoes = configuracoes ?? throw new ArgumentNullException(nameof(configuracoes));
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfiguracaoLojaResponseDto>> Obter(CancellationToken cancellationToken)
    {
        return Ok(await _configuracoes.ObterAsync(cancellationToken));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfiguracaoLojaResponseDto>> Atualizar(
        [FromBody] ConfiguracaoLojaUpdateDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _configuracoes.AtualizarAsync(dto, cancellationToken));
    }
}
