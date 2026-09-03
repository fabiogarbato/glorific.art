using Glorific.Application.Common;
using Glorific.Application.DTO.Pedidos;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Leitura e operacao manual sobre pedidos.
///
/// As consultas do cliente e as do painel sao metodos SEPARADOS, e nao um metodo com flag
/// "ehAdmin". Flag em assinatura de leitura e como IDOR nasce: basta um chamador esquecer de
/// passar false. Aqui o metodo do cliente sempre filtra por usuario dentro da consulta.
/// </summary>
public interface IPedidoService
{
    Task<PagedResult<PedidoResumoResponseDto>> ListarMeusAsync(
        string usuarioUuid,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Pedido de outra pessoa devolve 404, nunca 403 — 403 confirmaria que existe.</summary>
    Task<PedidoResponseDto> ObterMeuAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default);

    Task<RastreioResponseDto> ObterRastreioAsync(
        string usuarioUuid,
        string pedidoUuid,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PedidoResumoResponseDto>> ListarAdminAsync(
        PedidoFiltroAdminDto filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    Task<PedidoResponseDto> ObterAdminAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mudanca manual de status pela expedicao. Grava historico com o usuario responsavel —
    /// e o que responde "quem mudou este pedido" depois.
    /// </summary>
    Task<PedidoResponseDto> AlterarStatusAsync(
        string pedidoUuid,
        AlterarStatusPedidoDto dto,
        string usuarioAdminUuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela e devolve estoque conforme o estagio: pedido ainda nao pago libera RESERVA,
    /// pedido pago devolve ao FISICO. Cancelar etiqueta ja comprada e I/O de rede e acontece
    /// fora da transacao.
    /// </summary>
    Task<PedidoResponseDto> CancelarAsync(
        string pedidoUuid,
        CancelarPedidoDto dto,
        string usuarioAdminUuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Empurra a etiqueta sem esperar o ciclo do worker. Nao ha risco de etiqueta duplicada: o
    /// avanco usa o MESMO claim atomico do worker, e quem perde a corrida nao faz nada.
    ///
    /// Existe aqui, e nao no controller, para que o Id interno do pedido nunca precise atravessar
    /// a fronteira HTTP: o identificador publico e o Uuid, e so.
    /// </summary>
    Task<PedidoResponseDto> GerarEtiquetaAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// URL do PDF da etiqueta. publico = true gera link ABERTO e so deve sair do painel.
    /// </summary>
    Task<string?> ObterUrlEtiquetaAsync(
        string pedidoUuid,
        bool publico = false,
        CancellationToken cancellationToken = default);

    /// <summary>Sincroniza o rastreio sob demanda, para o atendimento nao esperar o worker.</summary>
    Task<PedidoResponseDto> SincronizarRastreioAsync(
        string pedidoUuid,
        CancellationToken cancellationToken = default);
}
