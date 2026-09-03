using System.Text.Json.Serialization;

namespace Glorific.Infrastructure.Integrations.InfinitePay;

/// <summary>
/// Corpo de POST {BaseUrl}/invoices/public/checkout/links.
///
/// Os nomes sao snake_case porque e o contrato da InfinitePay, e ficam declarados com
/// JsonPropertyName em vez de depender de uma politica global de serializacao: a API do projeto
/// serializa em camelCase, e uma politica global mudada por outro motivo quebraria silenciosamente
/// a integracao de pagamento.
/// </summary>
internal sealed record InfinitePayCheckoutRequest
{
    /// <summary>O @ do lojista. E a UNICA identificacao de conta — e e publica.</summary>
    [JsonPropertyName("handle")]
    public required string Handle { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<InfinitePayItem> Items { get; init; }

    /// <summary>
    /// Nosso identificador de correlacao. Volta no redirect e no webhook.
    /// NUNCA sequencial: o repo de referencia usava "loja-{id}" e qualquer um enumerava pedidos
    /// alheios. Aqui e "glo-{guid}", gerado no CheckoutService e persistido em
    /// pagamentos.provider_order_id antes desta chamada.
    /// </summary>
    [JsonPropertyName("order_nsu")]
    public required string OrderNsu { get; init; }

    [JsonPropertyName("redirect_url")]
    public required string RedirectUrl { get; init; }

    [JsonPropertyName("webhook_url")]
    public required string WebhookUrl { get; init; }

    [JsonPropertyName("customer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InfinitePayCustomer? Customer { get; init; }
}

/// <summary>Linha da cobranca. price e SEMPRE centavos inteiros — nunca reais decimais.</summary>
internal sealed record InfinitePayItem
{
    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }

    [JsonPropertyName("price")]
    public required int Price { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

internal sealed record InfinitePayCustomer
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("phone_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneNumber { get; init; }
}
