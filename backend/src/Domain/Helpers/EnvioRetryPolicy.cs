namespace Glorific.Domain.Helpers;

/// <summary>
/// Backoff exponencial do worker de envio. Herdado do cwbmaq_backend.
/// Ate 8 tentativas, teto de 6 h, janela total util de ~24 h — tempo suficiente
/// para o lojista recarregar a carteira do Melhor Envio quando o saldo acabou.
/// </summary>
public static class EnvioRetryPolicy
{
    public const int MaximoTentativas = 8;
    private static readonly TimeSpan Teto = TimeSpan.FromHours(6);
    private static readonly TimeSpan Base = TimeSpan.FromMinutes(2);

    public static TimeSpan Atraso(int tentativa)
    {
        if (tentativa <= 0) return Base;
        // 2, 4, 8, 16, 32, 64, 128, 256 min — truncado no teto de 6 h.
        var fator = Math.Pow(2, Math.Min(tentativa, 10));
        var atraso = TimeSpan.FromMinutes(Base.TotalMinutes * fator);
        return atraso > Teto ? Teto : atraso;
    }

    public static DateTime ProximaTentativa(DateTime agoraUtc, int tentativa) => agoraUtc.Add(Atraso(tentativa));

    public static bool EsgotouTentativas(int tentativas) => tentativas >= MaximoTentativas;
}
