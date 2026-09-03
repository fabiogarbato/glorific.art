using Glorific.Api.Common;
using Glorific.Application.DTO.Conta;
using Glorific.Application.DTO.Identidade;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Area logada do cliente: perfil e enderecos.
///
/// Nenhuma rota daqui recebe o id do dono. Ele sai SEMPRE de User.ObterUuid(), ou seja, da claim
/// de um token que a propria API assinou. E a diferenca entre "o cliente pede o seu endereco" e
/// "o cliente pede o endereco de numero 42": a segunda forma e a que vaza dado alheio quando
/// alguem esquece um WHERE.
///
/// Endereco que existe mas e de outra pessoa responde 404, nao 403 — 403 confirmaria que aquele
/// id existe e permitiria mapear a base por varredura.
/// </summary>
[ApiController]
[Route("api/v1/conta")]
[Produces("application/json")]
[Authorize]
public sealed class ContaController : ControllerBase
{
    private readonly IUsuarioService _usuarios;
    private readonly IEnderecoService _enderecos;

    public ContaController(IUsuarioService usuarios, IEnderecoService enderecos)
    {
        _usuarios = usuarios;
        _enderecos = enderecos;
    }

    // ------------------------------------------------------------------
    // Perfil
    // ------------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioResponseDto>> ObterPerfil(CancellationToken cancellationToken)
    {
        var perfil = await _usuarios.ObterPerfilAsync(User.ObterUuid(), cancellationToken);
        return Ok(perfil);
    }

    /// <summary>
    /// Atualiza os dados pessoais. E-mail nao entra: trocar e-mail exige reverificacao e por
    /// isso e um fluxo proprio, nao um campo escondido num PUT de perfil.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UsuarioResponseDto>> AtualizarPerfil(
        [FromBody] PerfilUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var perfil = await _usuarios.AtualizarPerfilAsync(User.ObterUuid(), dto, cancellationToken);
        return Ok(perfil);
    }

    // ------------------------------------------------------------------
    // Enderecos
    // ------------------------------------------------------------------

    [HttpGet("enderecos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EnderecoResponseDto>>> ListarEnderecos(
        CancellationToken cancellationToken)
    {
        var enderecos = await _enderecos.ListarAsync(User.ObterUuid(), cancellationToken);
        return Ok(enderecos);
    }

    [HttpGet("enderecos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnderecoResponseDto>> ObterEndereco(
        int id,
        CancellationToken cancellationToken)
    {
        var endereco = await _enderecos.ObterAsync(User.ObterUuid(), id, cancellationToken);
        return Ok(endereco);
    }

    [HttpPost("enderecos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnderecoResponseDto>> CriarEndereco(
        [FromBody] EnderecoCreateDto dto,
        CancellationToken cancellationToken)
    {
        var criado = await _enderecos.CriarAsync(User.ObterUuid(), dto, cancellationToken);

        return CreatedAtAction(nameof(ObterEndereco), new { id = criado.Id }, criado);
    }

    /// <summary>O id vem da rota; o corpo nao carrega id nem dono.</summary>
    [HttpPut("enderecos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnderecoResponseDto>> AtualizarEndereco(
        int id,
        [FromBody] EnderecoUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var atualizado = await _enderecos.AtualizarAsync(User.ObterUuid(), id, dto, cancellationToken);
        return Ok(atualizado);
    }

    [HttpDelete("enderecos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverEndereco(int id, CancellationToken cancellationToken)
    {
        await _enderecos.RemoverAsync(User.ObterUuid(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Promove o endereco a principal. So pode existir um por cliente.</summary>
    [HttpPut("enderecos/{id:int}/principal")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnderecoResponseDto>> DefinirEnderecoPrincipal(
        int id,
        CancellationToken cancellationToken)
    {
        var endereco = await _enderecos.DefinirPrincipalAsync(User.ObterUuid(), id, cancellationToken);
        return Ok(endereco);
    }
}
