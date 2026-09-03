using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class EnvioRepository : BaseRepository<Envio>, IEnvioRepository
{
    /// <summary>
    /// Janela de posse do worker sobre a linha. Enquanto ela nao vence, nenhum outro processo
    /// reivindica o mesmo envio. E deliberadamente maior que o tempo de uma chamada ao Melhor
    /// Envio e menor que o menor passo do backoff, para o retry normal nao ficar preso.
    /// </summary>
    private static readonly TimeSpan DuracaoLease = TimeSpan.FromMinutes(2);

    /// <summary>Status que ainda demandam trabalho do worker de etiqueta.</summary>
    private static readonly StatusEnvio[] StatusProcessaveis =
    [
        StatusEnvio.Pendente,
        StatusEnvio.NoCarrinho,
        StatusEnvio.Comprado
    ];

    public EnvioRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Envio?> ObterPorPedidoAsync(int idPedido, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.IdPedido == idPedido, cancellationToken);

    public Task<Envio?> ObterPorMeOrderIdAsync(string meOrderId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.MeOrderId == meOrderId, cancellationToken);

    /// <summary>
    /// Fila do worker de etiquetas: status ainda processavel e proxima tentativa ja vencida.
    /// Bate exatamente no indice ix_envios_status_proxima_tentativa. Envio que ja esgotou o
    /// numero de tentativas sai da fila — dali em diante e caso para o admin, nao para o retry.
    /// </summary>
    public async Task<IReadOnlyList<Envio>> ObterPendentesAsync(
        DateTime agoraUtc,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        return await Query()
            .Where(e => StatusProcessaveis.Contains(e.Status)
                        && e.Tentativas < EnvioRetryPolicy.MaximoTentativas
                        && (e.ProximaTentativaEm == null || e.ProximaTentativaEm <= agoraUtc))
            .OrderBy(e => e.ProximaTentativaEm)
            .ThenBy(e => e.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Claim atomico da linha antes de chamar o Melhor Envio.
    ///
    /// Sem ele, o worker e a acao manual do admin compram etiqueta para o mesmo pedido ao mesmo
    /// tempo e a segunda e cobrada da carteira sem nunca ser usada. O UPDATE condicional empurra
    /// ProximaTentativaEm para o fim do lease e incrementa Tentativas na mesma instrucao: quem
    /// perder a corrida ve zero linhas afetadas e desiste.
    ///
    /// ExecuteUpdateAsync nao atualiza o ChangeTracker — a linha e desanexada logo em seguida e
    /// o chamador precisa reconsultar (ObterParaEdicaoAsync) antes de gravar o resultado da
    /// chamada ao parceiro.
    /// </summary>
    public async Task<bool> TentarReivindicarAsync(
        int idEnvio,
        DateTime agoraUtc,
        CancellationToken cancellationToken = default)
    {
        var leaseAte = agoraUtc.Add(DuracaoLease);

        var linhas = await Contexto.Envios
            .Where(e => e.Id == idEnvio
                        && StatusProcessaveis.Contains(e.Status)
                        && e.Tentativas < EnvioRetryPolicy.MaximoTentativas
                        && (e.ProximaTentativaEm == null || e.ProximaTentativaEm <= agoraUtc))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Tentativas, e => e.Tentativas + 1)
                    .SetProperty(e => e.ProximaTentativaEm, leaseAte)
                    .SetProperty(e => e.DataAlteracao, agoraUtc),
                cancellationToken);

        DesanexarRastreados<Envio>(e => e.Id == idEnvio);
        return linhas > 0;
    }

    public async Task RegistrarEventoAsync(EnvioEvento evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await Contexto.EnviosEventos.AddAsync(evento, cancellationToken);
    }

    /// <summary>Linha do tempo do rastreio, do mais recente para o mais antigo.</summary>
    public async Task<IReadOnlyList<EnvioEvento>> ObterEventosAsync(
        int idEnvio,
        CancellationToken cancellationToken = default) =>
        await Contexto.EnviosEventos
            .AsNoTracking()
            .Where(e => e.IdEnvio == idEnvio)
            .OrderByDescending(e => e.OcorridoEm)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
}
