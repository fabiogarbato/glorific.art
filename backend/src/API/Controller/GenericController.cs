using Glorific.Application.Common;
using Glorific.Application.DTO;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Base CRUD dos controllers. Cinco actions virtual: quem herda sobrescreve so o que muda —
/// tipicamente para pendurar um [Authorize(Policy = ...)] diferente numa action especifica.
///
/// A rota NAO e declarada aqui. Cada controller concreto declara a sua, sempre versionada,
/// plural e minuscula: [Route("api/v1/produtos")]. No repo de referencia convivia
/// [Route("api/[controller]")] (PascalCase, singular) com rotas escritas a mao, e o front tinha
/// '/api/Categoria' e '/api/clientes' no mesmo arquivo.
///
/// Nao ha [Authorize] nem [AllowAnonymous] nesta classe de proposito: a FallbackPolicy do
/// Program.cs ja exige usuario autenticado por padrao, entao o controller concreto so precisa
/// declarar quando quer AFROUXAR (publico) ou APERTAR (policy administrativa).
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class GenericController<TEntity, TCreateDto, TUpdateDto, TResponseDto> : ControllerBase
    where TEntity : BaseEntity
    where TCreateDto : CreateDto
    where TUpdateDto : UpdateDto
    where TResponseDto : ResponseDto
{
    protected GenericController(IGenericService<TEntity, TCreateDto, TUpdateDto, TResponseDto> servico)
    {
        Servico = servico ?? throw new ArgumentNullException(nameof(servico));
    }

    protected IGenericService<TEntity, TCreateDto, TUpdateDto, TResponseDto> Servico { get; }

    /// <summary>
    /// Extrai o id do DTO de resposta para montar o Location do 201.
    ///
    /// ABSTRATO, e nao por reflection. O repo de referencia procurava uma propriedade chamada
    /// "Id" em runtime: DTO sem essa propriedade compilava normalmente e so quebrava na hora do
    /// POST, em producao. Aqui, esquecer de implementar e erro de compilacao.
    /// </summary>
    protected abstract int GetId(TResponseDto dto);

    /// <summary>Listagem paginada. Nunca devolve a colecao inteira.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<ActionResult<PagedResult<TResponseDto>>> Listar(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // PageRequest normaliza no construtor: page=0 vira 1 e pageSize=999999 vira o teto.
        var resultado = await Servico.ListarAsync(new PageRequest(page, pageSize), cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Detalhe por id. O 404 vem do servico, traduzido pelo middleware.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TResponseDto>> ObterPorId(int id, CancellationToken cancellationToken)
    {
        var resultado = await Servico.ObterPorIdAsync(id, cancellationToken);
        return Ok(resultado);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<TResponseDto>> Criar(
        [FromBody] TCreateDto dto,
        CancellationToken cancellationToken)
    {
        var criado = await Servico.CriarAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = GetId(criado) }, criado);
    }

    /// <summary>O id vem da ROTA. O UpdateDto nao carrega id — corpo nao pode contradizer a URL.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TResponseDto>> Atualizar(
        int id,
        [FromBody] TUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var atualizado = await Servico.AtualizarAsync(id, dto, cancellationToken);
        return Ok(atualizado);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
    {
        await Servico.RemoverAsync(id, cancellationToken);
        return NoContent();
    }
}
