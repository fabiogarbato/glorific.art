using Glorific.Application.DTO.Frete;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Cotacao de frete.
///
/// A cotacao e a chamada MAIS CARA do ciclo de compra: leva de 2 a 5 s no Melhor Envio e e
/// disparada na pagina de produto, no carrinho e no checkout — tres vezes pelo mesmo cliente,
/// pelos mesmos itens, para o mesmo CEP. Por isso o resultado e cacheado por 2 minutos por
/// (CEP de origem, CEP de destino, assinatura dos itens).
///
/// PESO, DIMENSAO E PRECO SAO LIDOS DO BANCO, nunca do corpo da requisicao. O cliente informa
/// apenas quais variacoes e quantas — aceitar peso vindo do navegador e aceitar frete forjado.
/// </summary>
public interface IFreteService
{
    /// <summary>Cotacao publica (pagina de produto e simulador do carrinho).</summary>
    Task<IReadOnlyList<OpcaoFreteResponseDto>> CotarAsync(
        CotacaoFreteRequestDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesma cotacao, com os itens ja resolvidos por quem chamou (tipicamente o carrinho do
    /// servidor). Existe para o controller do carrinho nao precisar reconstruir um DTO de
    /// entrada a partir de dados que ele ja tem em maos.
    /// </summary>
    Task<IReadOnlyList<OpcaoFreteResponseDto>> CotarItensAsync(
        string cep,
        IReadOnlyCollection<ItemCotacaoDto> itens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RECOTACAO server-side do servico escolhido — a defesa anti-fraude do checkout.
    ///
    /// O valor cobrado e SEMPRE o que volta daqui, nunca o que veio no corpo do checkout.
    /// Quando o servico escolhido some da cotacao (transportadora saiu do ar, rota deixou de
    /// ser atendida), lanca com a mensagem que manda o cliente refazer a escolha de frete.
    /// </summary>
    Task<OpcaoFreteResponseDto> RecotarServicoAsync(
        string cep,
        IReadOnlyCollection<ItemCotacaoDto> itens,
        int idServico,
        CancellationToken cancellationToken = default);
}
