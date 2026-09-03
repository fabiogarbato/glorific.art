using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Guia de medidas PUBLICO — o que alimenta a pagina /guia-de-medidas da loja.
///
/// [AllowAnonymous] EXPLICITO na classe: com a FallbackPolicy do Program, endpoint sem atributo
/// exige autenticacao. Aqui o anonimo e o ponto: guia de medidas atras de login e guia de
/// medidas que ninguem le — e ele existe justamente para reduzir devolucao por tamanho errado,
/// que acontece ANTES de a pessoa ter conta.
///
/// A leitura administrativa continua em /api/v1/admin/tabelas-medidas, com outro DTO. Sao dois
/// contratos separados de proposito: o publico nao devolve a flag Ativo nem o id da linha, e
/// nao passa a devolver no dia em que o painel ganhar um campo novo.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/tabelas-medidas")]
[Produces("application/json")]
public sealed class TabelasMedidasController : ControllerBase
{
    private readonly ITabelaMedidasService _tabelas;

    public TabelasMedidasController(ITabelaMedidasService tabelas)
    {
        _tabelas = tabelas;
    }

    /// <summary>
    /// Todas as tabelas ATIVAS, com as linhas na ordem da grade.
    ///
    /// Sem paginacao: a tela mostra o guia inteiro de uma vez, e paginar obrigaria o front a
    /// varrer paginas para montar uma unica pagina estatica.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TabelaMedidasPublicaDto>>> Listar(
        CancellationToken cancellationToken) =>
        Ok(await _tabelas.ListarPublicasAsync(cancellationToken));

    /// <summary>
    /// Uma tabela pelo id. Inativa responde 404, igual a inexistente: para quem esta fora do
    /// painel os dois casos sao a mesma coisa, e diferencia-los contaria o que ha la dentro.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabelaMedidasPublicaDto>> Obter(int id, CancellationToken cancellationToken) =>
        Ok(await _tabelas.ObterPublicaAsync(id, cancellationToken));
}
