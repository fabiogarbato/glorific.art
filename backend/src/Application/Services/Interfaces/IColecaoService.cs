using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface IColecaoService
    : IGenericService<Colecao, ColecaoCreateDto, ColecaoUpdateDto, ColecaoResponseDto>
{
    /// <summary>
    /// Habilitadas e dentro da janela DataInicio/DataFim segundo o IClock. E o que faz o drop
    /// agendado entrar na vitrine sozinho, sem ninguem apertar botao na madrugada.
    /// </summary>
    Task<IReadOnlyList<ColecaoResponseDto>> ObterVigentesAsync(CancellationToken cancellationToken = default);

    Task<ColecaoResponseDto> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Revincular so muda a ordem — nao duplica o vinculo.</summary>
    Task VincularProdutoAsync(
        int idColecao,
        VincularProdutoColecaoDto dto,
        CancellationToken cancellationToken = default);

    Task DesvincularProdutoAsync(int idColecao, int idProduto, CancellationToken cancellationToken = default);
}
