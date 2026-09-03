namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Contratacao de etiqueta no Melhor Envio.
///
/// A entidade Envio e simultaneamente agregado e FILA: o worker busca por status mais
/// ProximaTentativaEm. Este servico e a maquina de estados dessa fila, e ele PERSISTE DEPOIS DE
/// CADA PASSO — uma queda no meio retoma do estado real em vez de comprar a etiqueta duas vezes.
/// </summary>
public interface IEnvioService
{
    /// <summary>
    /// Cria a linha de envio do pedido pago. Nada e chamado no Melhor Envio aqui: so o INSERT.
    /// Servico que exige nota fiscal nasce em AguardandoNota e o worker nao o pega ate o admin
    /// informar a chave.
    ///
    /// Idempotente: a unique em envios.id_pedido e o que garante uma etiqueta por pedido.
    /// </summary>
    Task EnfileirarAsync(int idPedido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Um ciclo do worker. Reivindica cada envio da fila de forma atomica antes de qualquer I/O
    /// e devolve quantos foram processados. NUNCA lanca — worker que morre para de existir.
    /// </summary>
    Task<int> ProcessarPendentesAsync(int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Avanca um envio especifico pela maquina de estados. Usado pelo worker e pelo botao
    /// "gerar etiqueta" do painel — os dois competem pelo mesmo claim atomico.
    /// </summary>
    /// <returns>true quando o envio avancou de estado nesta execucao.</returns>
    Task<bool> ProcessarAsync(int idEnvio, CancellationToken cancellationToken = default);

    /// <summary>
    /// URL do PDF da etiqueta. Publico gera link aberto e so deve sair do botao do admin.
    /// Falha aqui nao regride o status: a etiqueta ja foi comprada, so o link faltou.
    /// </summary>
    Task<string?> ObterUrlEtiquetaAsync(
        int idPedido,
        bool publico = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sincroniza o rastreio. O status local so PROMOVE, nunca regride: o Melhor Envio reordena
    /// eventos, e deixar regredir faz um pedido entregue voltar para "postado" na tela do cliente.
    /// </summary>
    Task AtualizarRastreioAsync(int idEnvio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela a etiqueta no parceiro. Chamado FORA da transacao do banco por ser I/O de rede.
    /// </summary>
    Task<bool> CancelarAsync(
        int idPedido,
        string? descricao = null,
        CancellationToken cancellationToken = default);
}
