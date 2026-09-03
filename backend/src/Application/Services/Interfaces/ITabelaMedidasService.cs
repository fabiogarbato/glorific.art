using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface ITabelaMedidasService
    : IGenericService<TabelaMedidas, TabelaMedidasCreateDto, TabelaMedidasUpdateDto, TabelaMedidasResponseDto>
{
    /// <summary>Com as linhas na ordem do tamanho: a tabela e exibida inteira ou nao serve.</summary>
    Task<TabelaMedidasResponseDto> ObterComLinhasAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leitura PUBLICA (sem login) do guia de medidas: somente tabelas ativas, linhas ordenadas.
    ///
    /// Nao e paginada de proposito. As outras listagens da API sao, mas aqui o consumidor e a
    /// pagina /guia-de-medidas, que exibe TODAS as tabelas de uma vez; paginar obrigaria o front
    /// a varrer paginas so para montar uma tela unica, e o numero de tabelas de medidas de uma
    /// loja de moda fica na casa das unidades.
    /// </summary>
    Task<IReadOnlyList<TabelaMedidasPublicaDto>> ListarPublicasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uma tabela ATIVA pelo id, na forma publica. Lanca 404 quando nao existe OU esta inativa —
    /// distinguir os dois casos contaria ao visitante o que ha no painel.
    /// </summary>
    Task<TabelaMedidasPublicaDto> ObterPublicaAsync(int id, CancellationToken cancellationToken = default);
}
