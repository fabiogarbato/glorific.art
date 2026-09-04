using Glorific.Domain.Entities.Integracoes;

namespace Glorific.Domain.Interfaces.Repositories;

/// <summary>
/// Conta do Melhor Envio e linha unica (single tenant): so obter e alterar, igual
/// IConfiguracaoLojaRepository.
/// </summary>
public interface IContaMelhorEnvioRepository
{
    /// <summary>Leitura sem rastreamento — usada em toda chamada de negocio que precisa do token.</summary>
    Task<ContaMelhorEnvio?> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Rastreada, para o fluxo de OAuth alterar e o IUnitOfWork salvar.</summary>
    Task<ContaMelhorEnvio?> ObterParaEdicaoAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(ContaMelhorEnvio conta, CancellationToken cancellationToken = default);
}
