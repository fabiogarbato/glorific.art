using Glorific.Application.Models.MelhorEnvio;

namespace Glorific.Application.Ports;

/// <summary>
/// Porta de saida para o microservico integracaoMelhorEnvio (http://melhorenvio_api:8080,
/// header X-Api-Key). NAO e a API do Melhor Envio direta: o microservico faz o OAuth, renova o
/// token sozinho e repassa o corpo cru do ME.
///
/// Contrato da fronteira:
/// - o accountId (MelhorEnvio:ContaId) NAO aparece aqui — e detalhe do adaptador, que o le das
///   options. Serviço de negocio nao decide multi-tenancy do parceiro.
/// - nenhum JsonNode/JsonElement/HttpResponseMessage cruza esta interface. Se o dado interessa,
///   ele tem campo com nome nosso; o resto vai em RawJson (string) para jsonb.
/// - toda falha do parceiro vira MelhorEnvioApiException (definida na Infrastructure) — os
///   servicos tratam por excecao, nunca por status code, porque status code aqui e HTTP do ME
///   repassado, e um 404 pode significar "conta nao conectada", nao "nao existe".
///
/// Ordem real do fluxo (G.4): CotarFrete -> InserirNoCarrinho -> Comprar -> GerarEtiqueta ->
/// ImprimirEtiqueta -> Rastrear. Cada passo persiste ANTES do proximo, para que uma queda no
/// meio retome do estado real e nao compre a etiqueta duas vezes.
/// </summary>
public interface IMelhorEnvioClient
{
    /// <summary>
    /// POST /api/shipment/calculate — cotacao.
    ///
    /// Usada em dois lugares: vitrine/carrinho (sem login) e RECOTACAO server-side no checkout.
    /// Na recotacao vai apenas o servico escolhido; se ele sumir da resposta, o frete deixou de
    /// existir e o checkout para. O valor cobrado e SEMPRE o da recotacao, nunca o do body do
    /// cliente — e a defesa contra frete forjado.
    ///
    /// A lista ja vem normalizada (objeto unico virou lista, preco string virou centavos).
    /// Itens indisponiveis chegam com Erro preenchido em vez de sumirem.
    /// </summary>
    Task<IReadOnlyList<CotacaoFreteResultado>> CotarFreteAsync(
        CotacaoFreteRequisicao requisicao,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/cart (201) — insere o frete no carrinho do ME. Passo 1: Pendente -> NoCarrinho.
    /// Devolve o uuid da etiqueta (MeOrderId), que precisa ser persistido imediatamente.
    /// </summary>
    Task<CarrinhoEnvioResultado> InserirNoCarrinhoAsync(
        CarrinhoEnvioRequisicao requisicao,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/cart/checkout — paga a(s) etiqueta(s). Passo 2: NoCarrinho -> Comprado.
    /// CONSOME SALDO da carteira do ME; saldo insuficiente volta 4xx e o worker entra em backoff.
    /// Aceita varias etiquetas numa chamada so.
    /// </summary>
    Task<CompraEtiquetaResultado> ComprarAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/labels/generate — gera a etiqueta. Passo 3: Comprado -> EtiquetaGerada.
    /// Resposta e um mapa meOrderId -> { status, message }.
    /// </summary>
    Task<GeracaoEtiquetaResultado> GerarEtiquetaAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/labels/print — devolve a URL do PDF. Passo 4.
    ///
    /// <paramref name="modo"/> Privado e o padrao do worker; Publico gera link aberto e so deve
    /// ser usado no botao do admin. Falha aqui NAO regride o status do envio: a etiqueta ja foi
    /// comprada e gerada, so o link ficou faltando — loga warning e busca sob demanda depois.
    /// </summary>
    Task<ImpressaoEtiquetaResultado> ImprimirEtiquetaAsync(
        IReadOnlyList<string> meOrderIds,
        ModoImpressaoEtiqueta modo = ModoImpressaoEtiqueta.Privado,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/shipment/tracking — rastreio de uma ou varias etiquetas.
    /// O status local so promove, nunca regride.
    /// </summary>
    Task<IReadOnlyList<RastreioResultado>> RastrearAsync(
        IReadOnlyList<string> meOrderIds,
        CancellationToken ct = default);

    /// <summary>
    /// POST /api/shipment/cancel — cancela a etiqueta (reasonId sempre "2").
    /// Chamar FORA da transacao do banco: e I/O de rede e nao pode segurar lock.
    /// A devolucao de estoque e condicional ao item ainda nao ter sido despachado.
    /// </summary>
    Task<CancelamentoEtiquetaResultado> CancelarAsync(
        CancelamentoEtiquetaRequisicao requisicao,
        CancellationToken ct = default);

    /// <summary>
    /// GET /api/me/balance — saldo da carteira do ME.
    /// Monitorado para que "sem saldo" apareca como alerta antes de virar pedido pago com
    /// etiqueta em backoff.
    /// </summary>
    Task<SaldoMelhorEnvio> ConsultarSaldoAsync(CancellationToken ct = default);

    /// <summary>
    /// GET /api/auth/status — a conta esta autorizada?
    ///
    /// Nunca lanca por falta de token: devolve Conectada = false. E o healthcheck do go-live e o
    /// que distingue "conta precisa reautorizar" de "servico caiu" quando o resto da API comeca
    /// a responder 404 "Conta nao conectada".
    /// </summary>
    Task<StatusContaMelhorEnvio> VerificarStatusContaAsync(CancellationToken ct = default);
}
