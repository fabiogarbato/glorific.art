using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface IProdutoVariacaoService
    : IGenericService<ProdutoVariacao, ProdutoVariacaoCreateDto, ProdutoVariacaoUpdateDto, ProdutoVariacaoResponseDto>
{
    /// <summary>Grade do produto na ordem do seletor (cor, depois tamanho por Ordem).</summary>
    Task<IReadOnlyList<ProdutoVariacaoResponseDto>> ObterPorProdutoAsync(
        int idProduto,
        bool incluirInativas = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria todas as combinacoes FALTANTES de tamanhos x cores de uma vez.
    ///
    /// E o que torna o cadastro de moda viavel: 5 tamanhos x 4 cores sao 20 SKUs, e cadastrar um
    /// a um faz o admin desistir e vender tudo como tamanho unico. O que ja existe e preservado.
    /// </summary>
    Task<GradeGeradaDto> GerarGradeAsync(
        int idProduto,
        GerarGradeDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Reativa uma variacao desativada — que o filtro global esconde das consultas.</summary>
    Task<ProdutoVariacaoResponseDto> AtivarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Listagem administrativa incluindo variacoes desativadas.
    /// Sem isto o painel fica cego para o proprio soft delete.
    /// </summary>
    Task<PagedResult<ProdutoVariacaoResponseDto>> ListarAdminAsync(
        PageRequest requisicao,
        int? idProduto = null,
        string? busca = null,
        CancellationToken cancellationToken = default);
}
