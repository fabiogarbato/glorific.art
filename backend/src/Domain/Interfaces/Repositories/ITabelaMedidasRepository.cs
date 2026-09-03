using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ITabelaMedidasRepository : IBaseRepository<TabelaMedidas>
{
    /// <summary>Com as linhas na ordem do tamanho: a tabela e exibida inteira ou nao serve para nada.</summary>
    Task<TabelaMedidas?> ObterComLinhasAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Somente tabelas ATIVAS, com as linhas na ordem da grade. E o que a loja publica consome.
    ///
    /// O filtro de Ativo mora aqui, e nao no controller, porque "publico ve so o ativo" e regra
    /// de leitura do agregado: repetida em cada chamador, um dia alguem esquece e a tabela que o
    /// admin acabou de desativar volta a aparecer na vitrine.
    /// </summary>
    Task<IReadOnlyList<TabelaMedidas>> ListarAtivasComLinhasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uma tabela ATIVA pelo id, com as linhas na ordem da grade.
    /// Devolve null tambem quando a tabela existe mas esta inativa: para o publico, e a mesma
    /// coisa que nao existir — e responder 404 nos dois casos evita dizer "existe, mas voce nao
    /// pode ver".
    /// </summary>
    Task<TabelaMedidas?> ObterAtivaComLinhasAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> PossuiProdutosVinculadosAsync(int id, CancellationToken cancellationToken = default);
}
