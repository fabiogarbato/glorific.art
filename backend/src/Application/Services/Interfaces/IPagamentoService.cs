using Glorific.Application.Models.Pagamento;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Desfecho de um aviso de pagamento. Existe para o controller decidir o corpo da resposta sem
/// interpretar excecao — e para o log distinguir "nao pagou" (normal) de "pagou valor diferente"
/// (incidente que precisa de gente olhando).
/// </summary>
public enum ResultadoAvisoPagamento
{
    /// <summary>Reentrega do mesmo evento. Nada foi reprocessado; responder 200 e seguir.</summary>
    Duplicado = 1,

    /// <summary>Conferido no gateway, valor bateu, pedido marcado como pago.</summary>
    Aprovado = 2,

    /// <summary>Gateway respondeu, mas com status que nao aprova (recusado, expirado, cancelado).</summary>
    NaoAprovado = 3,

    /// <summary>
    /// Gateway aprovou um valor DIFERENTE do total do pedido. Nao aprova nada e marca para
    /// revisao manual. E o caso que o repo de referencia simplesmente nao verificava.
    /// </summary>
    DivergenciaDeValor = 4,

    /// <summary>
    /// Nao deu para conferir agora (gateway fora do ar, timeout). O evento fica NAO processado
    /// para uma nova tentativa; jamais se aprova por falta de resposta.
    /// </summary>
    Inconclusivo = 5,

    /// <summary>Nenhum pagamento nosso casa com este order_nsu. Provavel aviso forjado.</summary>
    PagamentoNaoEncontrado = 6
}

/// <summary>
/// Confirmacao de pagamento. Concentra a regra mais critica do sistema: nem o webhook nem o
/// redirect do navegador sao prova de pagamento.
/// </summary>
public interface IPagamentoService
{
    /// <summary>
    /// Ponto de entrada do webhook e do retorno do navegador.
    ///
    /// Ordem obrigatoria: grava o evento PRIMEIRO (a unique em provider_event_id transforma
    /// reentrega em 200 imediato), depois consulta o gateway, e so aprova se o gateway confirmar
    /// E o valor bater em centavos com o total do pedido.
    /// </summary>
    Task<ResultadoAvisoPagamento> ReceberAvisoAsync(
        WebhookPagamentoInfo aviso,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocessa eventos que ficaram sem desfecho (gateway indisponivel na primeira tentativa).
    /// Devolve quantos foram concluidos.
    /// </summary>
    Task<int> ProcessarEventosPendentesAsync(int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela pagamentos pendentes vencidos e LIBERA a reserva de estoque de cada item.
    /// Sem isto, pix abandonado prende peca para sempre. Devolve quantos foram expirados.
    /// </summary>
    Task<int> ExpirarPendentesAsync(int limite, CancellationToken cancellationToken = default);
}
