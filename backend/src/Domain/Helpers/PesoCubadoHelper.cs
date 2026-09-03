namespace Glorific.Domain.Helpers;

/// <summary>
/// Peso cubado dos Correios: max(peso_real, (C x L x A) / 6000).
/// Casaco de 800 g numa caixa 40x30x20 pesa 4 kg cubados — e a transportadora cobra o cubado.
/// Usado no relatorio de margem de frete, nao na cotacao (o Melhor Envio calcula sozinho).
/// </summary>
public static class PesoCubadoHelper
{
    public const int DivisorCorreios = 6000;

    public static decimal PesoCubadoGramas(decimal comprimentoCm, decimal larguraCm, decimal alturaCm, int divisor = DivisorCorreios)
    {
        if (comprimentoCm <= 0 || larguraCm <= 0 || alturaCm <= 0) return 0m;
        var cubadoKg = comprimentoCm * larguraCm * alturaCm / divisor;
        return cubadoKg * 1000m;
    }

    public static decimal PesoTaxavelGramas(decimal pesoRealGramas, decimal comprimentoCm, decimal larguraCm, decimal alturaCm, int divisor = DivisorCorreios)
        => Math.Max(pesoRealGramas, PesoCubadoGramas(comprimentoCm, larguraCm, alturaCm, divisor));
}
