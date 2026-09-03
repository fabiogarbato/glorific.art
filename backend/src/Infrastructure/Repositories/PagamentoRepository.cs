using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Glorific.Infrastructure.Repositories;

public sealed class PagamentoRepository : BaseRepository<Pagamento>, IPagamentoRepository
{
    /// <summary>unique_violation do Postgres. E o codigo que traduz "webhook repetido".</summary>
    private const string SqlStateUniqueViolation = "23505";

    public PagamentoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Pagamento?> ObterPorPedidoAsync(int idPedido, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.IdPedido == idPedido, cancellationToken);

    /// <summary>
    /// O webhook chega ora com o id do pedido no gateway, ora com o id da cobranca. Procurar so
    /// por um deixa evento orfao esperando um pagamento que existe — dai os dois metodos.
    /// </summary>
    public Task<Pagamento?> ObterPorProviderOrderIdAsync(
        string providerOrderId,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.ProviderOrderId == providerOrderId, cancellationToken);

    public Task<Pagamento?> ObterPorProviderChargeIdAsync(
        string providerChargeId,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.ProviderChargeId == providerChargeId, cancellationToken);

    /// <summary>
    /// Idempotencia de webhook decidida pelo BANCO, nao por um if no handler.
    ///
    /// O indice unico ux_pagamentos_eventos_provider_event_id e o arbitro: se o INSERT passa, o
    /// evento e novo; se volta 23505, a reentrega ja foi processada e o endpoint responde 200
    /// imediato em vez de reprocessar. Um "select antes de inserir" nao resolve — duas
    /// reentregas simultaneas passariam as duas pelo select e a segunda estouraria a violacao
    /// crua na cara do gateway, que reagiria reentregando de novo.
    ///
    /// ESTE E O UNICO PONTO DA CAMADA QUE PRECISA FLUSHAR: sem ir ao banco agora nao ha
    /// veredito de unicidade para devolver. Dentro de uma transacao aberta pelo caso de uso o EF
    /// cria savepoint antes do SaveChanges e volta a ele no erro, entao a violacao NAO aborta a
    /// transacao inteira. Por consequencia, chame este metodo com o ChangeTracker limpo: o flush
    /// carrega junto qualquer outra alteracao pendente.
    /// </summary>
    public async Task<bool> TentarRegistrarEventoAsync(
        PagamentoEvento evento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        await Contexto.PagamentosEventos.AddAsync(evento, cancellationToken);

        try
        {
            await Contexto.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (EhEventoDuplicado(ex))
        {
            // O evento repetido nao pode ficar pendurado no ChangeTracker: o proximo
            // SaveChanges do caso de uso tentaria inserir de novo e estouraria fora do try.
            Contexto.Entry(evento).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// Fila do worker: o webhook so grava e responde rapido, o processamento pesado vem depois.
    /// Ordem por RecebidoEm porque evento de tipo diferente pode chegar fora de ordem e o
    /// processador precisa aplicar na sequencia em que o gateway emitiu.
    /// </summary>
    public async Task<IReadOnlyList<PagamentoEvento>> ObterEventosNaoProcessadosAsync(
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        // RASTREADO de proposito, e esta e a unica consulta do repositorio que abre mao do
        // AsNoTracking. Quem consome esta lista precisa carimbar ProcessadoEm para o evento SAIR
        // da fila; com a entidade desanexada o carimbo nao chegava ao banco e o mesmo lote
        // voltava para sempre no topo do WHERE ProcessadoEm IS NULL, com os eventos novos
        // presos atras dele. Continua sem salvar: quem chama SaveChanges e o caso de uso.
        return await Contexto.PagamentosEventos
            .Where(e => e.ProcessadoEm == null)
            .OrderBy(e => e.RecebidoEm)
            .ThenBy(e => e.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Pagamentos pendentes com prazo vencido. Cancelar cada um libera a reserva de estoque —
    /// e o motivo de o worker existir: pix nao pago segura peca para sempre sem isso.
    /// </summary>
    public async Task<IReadOnlyList<Pagamento>> ObterExpiradosAsync(
        DateTime agoraUtc,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        return await Query()
            .Where(p => p.Status == StatusPagamento.Pendente
                        && p.ExpiraEm != null
                        && p.ExpiraEm <= agoraUtc)
            .OrderBy(p => p.ExpiraEm)
            .ThenBy(p => p.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A violacao de unicidade chega embrulhada: o EF lanca DbUpdateException e o codigo real
    /// esta na PostgresException interna.
    /// </summary>
    private static bool EhEventoDuplicado(DbUpdateException excecao) =>
        excecao.InnerException is PostgresException postgres
        && postgres.SqlState == SqlStateUniqueViolation;
}
