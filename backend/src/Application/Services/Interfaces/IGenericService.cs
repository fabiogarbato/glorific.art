using Glorific.Application.Common;
using Glorific.Application.DTO;
using Glorific.Domain.Common;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Contrato CRUD generico. Todo servico de agregado herda dele e declara APENAS o que tem de
/// extra: public interface IProdutoService : IGenericService&lt;Produto, ProdutoCreateDto,
/// ProdutoUpdateDto, ProdutoResponseDto&gt; { Task&lt;ProdutoResponseDto&gt; ObterPorSlugAsync(...); }
///
/// Todas as interfaces de servico moram nesta pasta. No repo de referencia 14 ficavam aqui e 12
/// inline no arquivo do service, e o resultado foi controller importando namespace diferente
/// dependendo do recurso.
/// </summary>
public interface IGenericService<TEntity, TCreate, TUpdate, TResponse>
    where TEntity : BaseEntity
    where TCreate : CreateDto
    where TUpdate : UpdateDto
    where TResponse : ResponseDto
{
    /// <summary>Listagem SEMPRE paginada. Nenhum caminho desta camada devolve a tabela inteira.</summary>
    Task<PagedResult<TResponse>> ListarAsync(PageRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Lanca EntityNotFoundException (404) quando nao existe.</summary>
    Task<TResponse> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TResponse> CriarAsync(TCreate dto, CancellationToken cancellationToken = default);

    /// <summary>O id vem da rota, nunca do corpo — senao o body pode contradizer a URL.</summary>
    Task<TResponse> AtualizarAsync(int id, TUpdate dto, CancellationToken cancellationToken = default);

    Task RemoverAsync(int id, CancellationToken cancellationToken = default);
}
