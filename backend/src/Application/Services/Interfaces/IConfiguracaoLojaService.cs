using Glorific.Application.DTO.Config;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Configuracao operacional da loja: linha unica, lida o tempo todo, escrita quase nunca.
///
/// Nao herda IGenericService porque nao existe listar, criar nem remover — o repositorio
/// correspondente nem herda IBaseRepository. Sao duas operacoes: obter e alterar.
///
/// A leitura e cacheada em memoria porque acontece em TODA cotacao de frete e em toda pagina de
/// produto; sem cache, cada visita a vitrine vira um SELECT numa tabela de uma linha so.
/// O cache e invalidado no save, e nao apenas por tempo: o admin que muda o prazo de manuseio
/// espera ver o efeito na proxima cotacao, nao daqui a dez minutos.
/// </summary>
public interface IConfiguracaoLojaService
{
    /// <summary>Leitura cacheada. Lanca 404 quando a linha de configuracao nao existe.</summary>
    Task<ConfiguracaoLojaResponseDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Altera e invalida o cache no mesmo passo.</summary>
    Task<ConfiguracaoLojaResponseDto> AtualizarAsync(
        ConfiguracaoLojaUpdateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta o valor cacheado. Existe para o caso de a configuracao ser alterada por fora
    /// (seed, migracao de dados, script de suporte) sem passar por AtualizarAsync.
    /// </summary>
    void InvalidarCache();
}
