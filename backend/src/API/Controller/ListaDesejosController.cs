using Glorific.Api.Common;
using Glorific.Application.DTO.Clientes;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Lista de desejos do cliente autenticado.
///
/// [Authorize] na CLASSE: nao existe rota publica aqui. E, mais importante, nenhuma rota recebe o
/// id do dono — ele sai sempre do token. Aceitar "idUsuario" na rota ou no corpo seria oferecer a
/// lista de desejos alheia a quem trocar um numero.
///
/// A remocao e por ID DE PRODUTO, e nao pelo id da linha: a chave de negocio da lista e o par
/// (usuario, produto), e assim o front nao precisa guardar um id que nao usa para mais nada.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize]
[Route("api/v1/lista-desejos")]
public class ListaDesejosController : ControllerBase
{
    private readonly IListaDesejoService _listaDesejos;
    private readonly IIdentidadeUsuarioService _identidade;

    public ListaDesejosController(IListaDesejoService listaDesejos, IIdentidadeUsuarioService identidade)
    {
        _listaDesejos = listaDesejos ?? throw new ArgumentNullException(nameof(listaDesejos));
        _identidade = identidade ?? throw new ArgumentNullException(nameof(identidade));
    }

    /// <summary>Lista completa, com capa, preco e disponibilidade. Nao e paginada: e curta.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ListaDesejoItemResponseDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        return Ok(await _listaDesejos.ListarAsync(idUsuario, cancellationToken));
    }

    /// <summary>
    /// So os ids de produto. E o que pinta o coracao em todos os cards da vitrine sem uma
    /// requisicao por card.
    /// </summary>
    [HttpGet("ids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<int>>> ListarIds(CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        return Ok(await _listaDesejos.ObterIdsProdutoAsync(idUsuario, cancellationToken));
    }

    /// <summary>Idempotente: favoritar o que ja esta na lista devolve 200 com o item existente.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListaDesejoItemResponseDto>> Adicionar(
        [FromBody] ListaDesejoCreateDto dto,
        CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        var item = await _listaDesejos.AdicionarAsync(idUsuario, dto, cancellationToken);

        // 200 e nao 201 de proposito: a operacao e idempotente e o chamador nao consegue
        // distinguir "criei agora" de "ja existia" — prometer 201 nas duas seria mentira.
        return Ok(item);
    }

    /// <summary>
    /// Toggle do coracao. Devolve true quando passou a fazer parte da lista e false quando saiu.
    /// </summary>
    [HttpPost("alternar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> Alternar(
        [FromBody] ListaDesejoCreateDto dto,
        CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        return Ok(await _listaDesejos.AlternarAsync(idUsuario, dto, cancellationToken));
    }

    /// <summary>404 quando o produto nao esta na lista DESTE usuario.</summary>
    [HttpDelete("produtos/{idProduto:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int idProduto, CancellationToken cancellationToken)
    {
        var idUsuario = await UsuarioAtualAsync(cancellationToken);

        await _listaDesejos.RemoverAsync(idUsuario, idProduto, cancellationToken);

        return NoContent();
    }

    private Task<int> UsuarioAtualAsync(CancellationToken cancellationToken) =>
        _identidade.ObterIdPorUuidAsync(User.ObterUuid(), cancellationToken);
}
