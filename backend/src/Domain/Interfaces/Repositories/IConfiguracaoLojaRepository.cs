using Glorific.Domain.Entities.Config;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Configuracao da loja e linha unica, entao nao existe listar nem remover: so obter e alterar.
/// </summary>
public interface IConfiguracaoLojaRepository
{
    /// <summary>Leitura sem rastreamento. E chamada em toda cotacao de frete e toda pagina de produto.</summary>
    Task<ConfiguracaoLoja?> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Rastreada, para o caso de uso do painel alterar e o IUnitOfWork salvar.</summary>
    Task<ConfiguracaoLoja?> ObterParaEdicaoAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(ConfiguracaoLoja configuracao, CancellationToken cancellationToken = default);
}
