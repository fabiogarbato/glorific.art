namespace Glorific.Application.Ports.Options;

/// <summary>
/// O valor que o appsettings VERSIONADO usa no lugar de um segredo real.
///
/// Fonte da verdade unica de "isto NAO foi configurado". Tratar somente string vazia como
/// ausente deixa o placeholder passar batido — a API sobe, a integracao "existe", e a falha so
/// aparece na cara do cliente, disfarcada de credencial invalida.
///
/// Mora na Application porque quem precisa dele esta nas duas pontas: o fail-fast do boot
/// (API/Common/RequiredSecret.cs) e os adaptadores da Infrastructure, que voltam a conferir em
/// runtime — configuracao pode vir de IOptionsMonitor e mudar depois do boot.
/// </summary>
public static class SegredoPlaceholder
{
    public const string Valor = "!!NO_KEY_PROVIDED!!";

    /// <summary>
    /// True quando o valor esta em branco ou ainda e o placeholder versionado.
    /// </summary>
    public static bool NaoConfigurado(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || string.Equals(valor.Trim(), Valor, StringComparison.Ordinal);
}
