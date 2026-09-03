namespace Glorific.Application.Models.MelhorEnvio;

/// <summary>
/// Resposta de GET {ME}/api/me/balance.
/// A compra da etiqueta (POST /api/cart/checkout) debita desta carteira. Saldo zerado nao da
/// erro no checkout do cliente: da erro DEPOIS, no worker, com o pedido ja pago — por isso o
/// saldo e monitorado e vira alerta operacional, nao surpresa.
/// </summary>
public sealed record SaldoMelhorEnvio
{
    public int SaldoCentavos { get; init; }

    /// <summary>"BRL" / "R$", como o ME devolver.</summary>
    public string? Moeda { get; init; }

    public string? RawJson { get; init; }
}

/// <summary>
/// Resposta de GET {ME}/api/auth/status (o unico contrato TIPADO do microservico; os demais sao
/// passthrough cru do Melhor Envio).
///
/// Nunca 404: conta sem token volta Conectada = false. E o oposto do resto da API, onde chamar
/// um recurso sem conta conectada devolve 404 "Conta nao conectada" — que deve virar alerta
/// operacional, nao 404 para o cliente final.
///
/// Access token e refresh token NUNCA sao expostos por este endpoint, de proposito.
/// </summary>
public sealed record StatusContaMelhorEnvio
{
    public bool Conectada { get; init; }

    /// <summary>accountId resolvido pelo microservico (MelhorEnvio:ContaId, ex.: "glorific").</summary>
    public string? ContaId { get; init; }

    /// <summary>"Bearer". Null quando desconectada.</summary>
    public string? TipoToken { get; init; }

    /// <summary>Escopos concedidos, separados por espaco.</summary>
    public string? Escopo { get; init; }

    public DateTimeOffset? ExpiraEmUtc { get; init; }

    public long? ExpiraEmSegundos { get; init; }

    /// <summary>true quando faltam menos de 5 minutos para expirar (skew do microservico).</summary>
    public bool PrecisaRenovar { get; init; }
}
