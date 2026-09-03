using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// Peso cubado dos Correios: max(peso_real, C x L x A / 6000). A transportadora cobra o cubado,
/// e e por isso que caixa grande e leve corroi a margem do frete.
///
/// Os parametros de [Theory] sao double com sufixo "d" de proposito: decimal nao e tipo valido
/// de argumento de atributo, e assim o valor chega ao teste sem depender de conversao do runner.
/// </summary>
public class PesoCubadoHelperTests
{
    /// <summary>
    /// O caso do casaco: 800 g numa caixa 40x30x20 cm pesa 4 kg cubados.
    /// 40 * 30 * 20 = 24000; 24000 / 6000 = 4 kg = 4000 g.
    /// </summary>
    [Fact]
    public void PesoTaxavelGramas_CasacoDeOitocentosGramasEmCaixaGrande_CobraQuatroKgCubados()
    {
        var taxavel = PesoCubadoHelper.PesoTaxavelGramas(800m, 40m, 30m, 20m);

        Assert.Equal(4000m, taxavel);
    }

    [Fact]
    public void PesoCubadoGramas_CaixaQuarentaPorTrintaPorVinte_RetornaQuatroMilGramas()
    {
        Assert.Equal(4000m, PesoCubadoHelper.PesoCubadoGramas(40m, 30m, 20m));
    }

    [Theory]
    [InlineData(10d, 10d, 10d, 166.6667d)]        // 1000 / 6000 kg
    [InlineData(20d, 20d, 20d, 1333.3333d)]       // 8000 / 6000 kg
    [InlineData(30d, 30d, 30d, 4500d)]            // 27000 / 6000 kg
    [InlineData(16d, 11d, 2d, 58.6667d)]          // caixa minima dos Correios
    [InlineData(100d, 100d, 100d, 166666.6667d)]
    public void PesoCubadoGramas_DimensoesVariadas_AplicaODivisorSeisMil(
        double comprimento, double largura, double altura, double esperadoGramas)
    {
        var cubado = PesoCubadoHelper.PesoCubadoGramas(
            (decimal)comprimento, (decimal)largura, (decimal)altura);

        Assert.Equal((decimal)esperadoGramas, cubado, 4);
    }

    /// <summary>
    /// Dimensao zerada (produto sem medida cadastrada) devolve 0 em vez de estourar divisao
    /// ou virar um cubado absurdo. O peso real e quem manda nesse caso.
    /// </summary>
    [Theory]
    [InlineData(0d, 30d, 20d)]
    [InlineData(40d, 0d, 20d)]
    [InlineData(40d, 30d, 0d)]
    [InlineData(0d, 0d, 0d)]
    public void PesoCubadoGramas_AlgumaDimensaoZerada_RetornaZero(double c, double l, double a)
    {
        Assert.Equal(0m, PesoCubadoHelper.PesoCubadoGramas((decimal)c, (decimal)l, (decimal)a));
    }

    [Theory]
    [InlineData(-40d, 30d, 20d)]
    [InlineData(40d, -30d, 20d)]
    [InlineData(40d, 30d, -20d)]
    [InlineData(-1d, -1d, -1d)] // produto de tres negativos daria cubado negativo; a guarda corta antes
    public void PesoCubadoGramas_AlgumaDimensaoNegativa_RetornaZero(double c, double l, double a)
    {
        Assert.Equal(0m, PesoCubadoHelper.PesoCubadoGramas((decimal)c, (decimal)l, (decimal)a));
    }

    [Theory]
    [InlineData(0d, 30d, 20d)]
    [InlineData(40d, 0d, 20d)]
    [InlineData(40d, 30d, 0d)]
    [InlineData(-40d, 30d, 20d)]
    public void PesoTaxavelGramas_DimensaoInvalida_CaiNoPesoReal(double c, double l, double a)
    {
        Assert.Equal(800m, PesoCubadoHelper.PesoTaxavelGramas(800m, (decimal)c, (decimal)l, (decimal)a));
    }

    /// <summary>Bloco de ferro pequeno: o peso real ganha e o cubado e ignorado.</summary>
    [Theory]
    [InlineData(5000d, 5000d)]  // real 5 kg > cubado 4 kg
    [InlineData(4001d, 4001d)]  // um grama acima do cubado
    [InlineData(4000d, 4000d)]  // empate exato
    [InlineData(3999d, 4000d)]  // um grama abaixo: o cubado assume
    [InlineData(0d, 4000d)]     // sem peso real cadastrado
    public void PesoTaxavelGramas_ComparaRealComCubadoDaCaixaPadrao_DevolveOMaior(
        double pesoReal, double esperado)
    {
        var taxavel = PesoCubadoHelper.PesoTaxavelGramas((decimal)pesoReal, 40m, 30m, 20m);

        Assert.Equal((decimal)esperado, taxavel);
    }

    [Fact]
    public void PesoTaxavelGramas_PesoRealZeradoESemDimensao_RetornaZero()
    {
        Assert.Equal(0m, PesoCubadoHelper.PesoTaxavelGramas(0m, 0m, 0m, 0m));
    }

    /// <summary>
    /// Transportadora com divisor 5000 (padrao aereo) cobra mais que os 6000 dos Correios
    /// pela mesma caixa. O parametro existe justamente para o relatorio comparar as duas.
    /// </summary>
    [Fact]
    public void PesoCubadoGramas_DivisorCincoMil_CobraMaisQueODivisorPadrao()
    {
        var aereo = PesoCubadoHelper.PesoCubadoGramas(40m, 30m, 20m, divisor: 5000);
        var correios = PesoCubadoHelper.PesoCubadoGramas(40m, 30m, 20m);

        Assert.Equal(4800m, aereo);
        Assert.Equal(4000m, correios);
        Assert.True(aereo > correios);
    }

    [Fact]
    public void PesoTaxavelGramas_DivisorCustomizado_UsaODivisorInformado()
    {
        Assert.Equal(4800m, PesoCubadoHelper.PesoTaxavelGramas(800m, 40m, 30m, 20m, divisor: 5000));
    }

    [Fact]
    public void DivisorCorreios_ValorPadrao_EhSeisMil()
    {
        Assert.Equal(6000, PesoCubadoHelper.DivisorCorreios);
    }

    /// <summary>
    /// Dobrar so uma aresta dobra o cubado — e a razao de a caixa errada custar o dobro
    /// de frete carregando o mesmo produto.
    /// </summary>
    [Fact]
    public void PesoCubadoGramas_DobrandoUmaAresta_DobraOCubado()
    {
        var normal = PesoCubadoHelper.PesoCubadoGramas(40m, 30m, 20m);
        var dobrada = PesoCubadoHelper.PesoCubadoGramas(80m, 30m, 20m);

        Assert.Equal(normal * 2m, dobrada);
    }

    [Fact]
    public void PesoTaxavelGramas_NuncaEhMenorQueOPesoReal()
    {
        decimal[] pesos = [0m, 1m, 800m, 4000m, 30000m];
        foreach (var peso in pesos)
        {
            var taxavel = PesoCubadoHelper.PesoTaxavelGramas(peso, 40m, 30m, 20m);
            Assert.True(taxavel >= peso, $"taxavel {taxavel} ficou abaixo do peso real {peso}");
        }
    }
}
