using Glorific.Api.Common;
using Glorific.Application.Common;
using Glorific.Application.DTO.Identidade;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller.Admin;

/// <summary>
/// Administracao de usuarios e papeis.
///
/// SomenteAdmin na CLASSE, e nao por action: gerente e operador nao entram aqui de forma
/// nenhuma. Conceder papel e a operacao mais perigosa do sistema — quem consegue conceder
/// "admin" consegue tudo o mais — e por isso ela nao compartilha policy com a gestao do
/// catalogo.
///
/// A segunda trava esta no servico: ninguem altera os proprios papeis nem desativa a propria
/// conta. Ela fica la, e nao aqui, porque e regra de negocio e precisa valer para qualquer
/// chamador futuro (um comando de manutencao, um teste), nao so para este controller.
/// </summary>
[ApiController]
[Route("api/v1/admin/usuarios")]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacao.SomenteAdmin)]
public sealed class UsuariosAdminController : ControllerBase
{
    private readonly IUsuarioService _usuarios;

    public UsuariosAdminController(IUsuarioService usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>Listagem paginada. Filtra por texto (e-mail, nome, CPF), papel e situacao.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UsuarioResponseDto>>> Listar(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        [FromQuery] string? papel,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        // PageRequest normaliza no construtor: page=0 vira 1 e pageSize=999999 vira o teto.
        var resultado = await _usuarios.ListarAsync(
            new PageRequest(page, pageSize), search, papel, ativo, cancellationToken);

        return Ok(resultado);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> ObterPorId(int id, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ObterPorIdAsync(id, cancellationToken);
        return Ok(usuario);
    }

    /// <summary>Edita dados cadastrais. Papel e situacao tem endpoints proprios.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> Atualizar(
        int id,
        [FromBody] UsuarioAdminUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var atualizado = await _usuarios.AtualizarAsync(id, dto, cancellationToken);
        return Ok(atualizado);
    }

    /// <summary>
    /// Concede um papel. Idempotente: conceder duas vezes nao estoura violacao de chave.
    /// </summary>
    [HttpPost("{id:int}/roles/{papel}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> ConcederPapel(
        int id,
        string papel,
        CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ConcederPapelAsync(id, papel, User.ObterUuid(), cancellationToken);
        return Ok(usuario);
    }

    /// <summary>Revoga um papel e derruba as sessoes do alvo para o privilegio nao sobreviver.</summary>
    [HttpDelete("{id:int}/roles/{papel}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> RevogarPapel(
        int id,
        string papel,
        CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.RevogarPapelAsync(id, papel, User.ObterUuid(), cancellationToken);
        return Ok(usuario);
    }

    /// <summary>Desativa (soft delete) e revoga todas as sessoes do usuario.</summary>
    [HttpPost("{id:int}/desativar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> Desativar(int id, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.DesativarAsync(id, User.ObterUuid(), cancellationToken);
        return Ok(usuario);
    }

    [HttpPost("{id:int}/ativar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponseDto>> Ativar(int id, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.AtivarAsync(id, User.ObterUuid(), cancellationToken);
        return Ok(usuario);
    }
}
