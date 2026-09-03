namespace Glorific.Application.Models.Pagamento;

/// <summary>
/// Pedido de criacao de cobranca, agnostico de gateway.
///
/// Modelado para servir a InfinitePay (checkout web hospedado, sem chave secreta, identificacao
/// por handle publico) SEM amarrar o contrato a ela: nada aqui menciona handle, order_nsu no
/// formato dela ou qualquer campo exclusivo. Trocar por Pagar.me, Asaas ou Mercado Pago exige
/// escrever outro adaptador, nao mexer nesta porta.
/// </summary>
public sealed record CheckoutRequisicaoInfo
{
    /// <summary>
    /// Nosso identificador de correlacao (order_nsu na InfinitePay). Volta no redirect e no
    /// webhook, e e a chave da conferencia.
    ///
    /// ATENCAO (falha #3 do geb-sul): NUNCA sequencial. Use "pedido-{uuid}" — order_nsu
    /// previsivel deixa qualquer um forjar um retorno de pagamento para um pedido alheio.
    /// </summary>
    public required string OrderNsu { get; init; }

    /// <summary>
    /// Linhas da cobranca. O FRETE entra como linha propria de valor flat, fora de qualquer
    /// multiplicador ou desconto de metodo de pagamento.
    /// </summary>
    public IReadOnlyList<CheckoutItemInfo> Itens { get; init; } = [];

    /// <summary>Para onde o navegador do cliente volta (App:PublicBaseUrl + rota de retorno).</summary>
    public required string UrlRetorno { get; init; }

    /// <summary>Notificacao server-to-server (App:PublicBaseUrl + /api/v1/webhooks/pagamento).</summary>
    public required string UrlWebhook { get; init; }

    public CheckoutClienteInfo? Cliente { get; init; }

    /// <summary>Total esperado em centavos. Serve de conferencia contra a soma dos itens.</summary>
    public int TotalCentavos { get; init; }
}

/// <summary>Uma linha da cobranca. Preco SEMPRE em centavos inteiros — nunca decimal, nunca double.</summary>
public sealed record CheckoutItemInfo
{
    public required string Descricao { get; init; }

    public required int Quantidade { get; init; }

    public required int PrecoUnitarioCentavos { get; init; }

    public int TotalCentavos => Quantidade * PrecoUnitarioCentavos;
}

public sealed record CheckoutClienteInfo
{
    public string? Nome { get; init; }

    public string? Email { get; init; }

    /// <summary>So digitos, com DDD.</summary>
    public string? Telefone { get; init; }

    /// <summary>CPF, so digitos.</summary>
    public string? Documento { get; init; }
}

/// <summary>
/// Resultado da criacao da cobranca.
///
/// Sucesso = false faz o CheckoutOrchestrator lancar e dar rollback em TUDO (pedido criado e
/// reserva de estoque). Nunca comitar pedido sem link de pagamento.
/// </summary>
public sealed record CheckoutCriadoInfo
{
    public required bool Sucesso { get; init; }

    /// <summary>URL da pagina de pagamento hospedada. Null quando Sucesso = false.</summary>
    public string? UrlCheckout { get; init; }

    /// <summary>Ecoa o OrderNsu efetivamente registrado no gateway.</summary>
    public string? OrderNsu { get; init; }

    /// <summary>Id da cobranca no gateway, quando ele devolve um. Vai em pagamentos.provider_charge_id.</summary>
    public string? ProviderChargeId { get; init; }

    /// <summary>Copia e cola do pix, quando o gateway ja entrega no ato.</summary>
    public string? QrCodePix { get; init; }

    public string? LinhaDigitavel { get; init; }

    public DateTime? ExpiraEmUtc { get; init; }

    /// <summary>Mensagem de falha ja tratada para log. Nunca exibir crua ao cliente.</summary>
    public string? Erro { get; init; }

    /// <summary>Payload cru para pagamentos.raw_ultima_resposta (jsonb).</summary>
    public string? RawJson { get; init; }

    public static CheckoutCriadoInfo Falha(string erro, string? rawJson = null) =>
        new() { Sucesso = false, Erro = erro, RawJson = rawJson };
}
