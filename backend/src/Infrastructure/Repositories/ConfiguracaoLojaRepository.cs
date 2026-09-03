using Glorific.Domain.Entities.Config;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

/// <summary>
/// Configuracao da loja e linha unica: nao herda BaseRepository porque nao existe listar,
/// paginar nem remover. So obter e alterar.
/// </summary>
public sealed class ConfiguracaoLojaRepository : IConfiguracaoLojaRepository
{
    private readonly GlorificContext _contexto;

    public ConfiguracaoLojaRepository(GlorificContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <summary>
    /// Sem rastreamento: e chamada em toda cotacao de frete e toda pagina de produto, e nenhuma
    /// delas altera nada. OrderBy(Id) para a leitura ser deterministica mesmo se alguem inserir
    /// uma segunda linha a mao no banco.
    /// </summary>
    public Task<ConfiguracaoLoja?> ObterAsync(CancellationToken cancellationToken = default) =>
        _contexto.ConfiguracoesLoja
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Rastreada, para o caso de uso do painel alterar e o IUnitOfWork salvar.</summary>
    public Task<ConfiguracaoLoja?> ObterParaEdicaoAsync(CancellationToken cancellationToken = default) =>
        _contexto.ConfiguracoesLoja
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AdicionarAsync(
        ConfiguracaoLoja configuracao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        await _contexto.ConfiguracoesLoja.AddAsync(configuracao, cancellationToken);
    }
}
