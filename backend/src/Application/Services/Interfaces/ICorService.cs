using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface ICorService
    : IGenericService<Cor, CorCreateDto, CorUpdateDto, CorResponseDto>
{
    Task<IReadOnlyList<CorResponseDto>> ObterAtivasOrdenadasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cores que de fato tem variacao no produto — o seletor de swatch da PDP.</summary>
    Task<IReadOnlyList<CorResponseDto>> ObterDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default);
}
