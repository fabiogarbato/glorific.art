using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;

namespace Glorific.Application.Services.Interfaces;

public interface ITamanhoService
    : IGenericService<Tamanho, TamanhoCreateDto, TamanhoUpdateDto, TamanhoResponseDto>
{
    /// <summary>Ordenado por Ordem, nunca alfabeticamente: senao GG aparece antes de P.</summary>
    Task<IReadOnlyList<TamanhoResponseDto>> ObterAtivosOrdenadosAsync(
        GradeTamanho? grade = null,
        CancellationToken cancellationToken = default);
}
