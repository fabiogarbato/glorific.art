using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface ICategoriaService
    : IGenericService<Categoria, CategoriaCreateDto, CategoriaUpdateDto, CategoriaResponseDto>
{
    /// <summary>Arvore de UM nivel (pai + filhas). E o menu do site inteiro numa consulta.</summary>
    Task<IReadOnlyList<CategoriaResponseDto>> ObterArvoreAsync(
        bool somenteHabilitadas = true,
        CancellationToken cancellationToken = default);

    /// <summary>Lanca 404 quando o slug nao existe.</summary>
    Task<CategoriaResponseDto> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);
}
