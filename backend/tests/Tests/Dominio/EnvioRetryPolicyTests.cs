using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// Backoff do worker de envio. O que esta em jogo: quando a carteira do Melhor Envio zera,
/// o worker precisa insistir por horas — mas sem virar tempestade de requisicao nem desistir
/// antes de o lojista conseguir recarregar.
/// </summary>
public class EnvioRetryPolicyTests
{
    [Fact]
    public void MaximoTentativas_ValorConfigurado_EhOito()
    {
        Assert.Equal(8, EnvioRetryPolicy.MaximoTentativas);
    }

    /// <summary>
    /// Base de 2 min dobrando a cada tentativa: 4, 8, 16, 32, 64, 128, 256 min.
    /// A tentativa 0 (primeira falha, nenhuma repeticao ainda) usa a base crua de 2 min.
    /// </summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    [InlineData(4, 32)]
    [InlineData(5, 64)]
    [InlineData(6, 128)]
    [InlineData(7, 256)]
    public void Atraso_TentativaAbaixoDoTeto_CresceExponencialmente(int tentativa, int minutosEsperados)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutosEsperados), EnvioRetryPolicy.Atraso(tentativa));
    }

    [Fact]
    public void Atraso_TentativaZeroOuNegativa_UsaAEsperaBaseDeDoisMinutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), EnvioRetryPolicy.Atraso(0));
        Assert.Equal(TimeSpan.FromMinutes(2), EnvioRetryPolicy.Atraso(-1));
        Assert.Equal(TimeSpan.FromMinutes(2), EnvioRetryPolicy.Atraso(int.MinValue));
    }

    /// <summary>
    /// A partir da 8a tentativa o dobro passaria de 8 h. O teto de 6 h existe para o worker
    /// nao dormir um turno inteiro depois de uma falha transitoria.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(50)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void Atraso_TentativaAcimaDoTeto_TruncaEmSeisHoras(int tentativa)
    {
        Assert.Equal(TimeSpan.FromHours(6), EnvioRetryPolicy.Atraso(tentativa));
    }

    [Fact]
    public void Atraso_UltimaTentativaAbaixoDoTeto_AindaNaoEstaTruncada()
    {
        // 256 min = 4 h 16 min: e o maior atraso antes de o teto entrar em cena.
        Assert.Equal(TimeSpan.FromMinutes(256), EnvioRetryPolicy.Atraso(7));
        Assert.True(EnvioRetryPolicy.Atraso(7) < TimeSpan.FromHours(6));
        Assert.Equal(TimeSpan.FromHours(6), EnvioRetryPolicy.Atraso(8));
    }

    [Fact]
    public void Atraso_SequenciaDeTentativas_NuncaDiminui()
    {
        var anterior = EnvioRetryPolicy.Atraso(0);
        for (var tentativa = 1; tentativa <= 20; tentativa++)
        {
            var atual = EnvioRetryPolicy.Atraso(tentativa);
            Assert.True(atual >= anterior,
                $"atraso da tentativa {tentativa} ({atual}) ficou menor que o da anterior ({anterior})");
            anterior = atual;
        }
    }

    [Fact]
    public void Atraso_QualquerTentativa_FicaEntreABaseEOTeto()
    {
        for (var tentativa = -5; tentativa <= 30; tentativa++)
        {
            var atraso = EnvioRetryPolicy.Atraso(tentativa);
            Assert.True(atraso >= TimeSpan.FromMinutes(2), $"tentativa {tentativa} devolveu {atraso}");
            Assert.True(atraso <= TimeSpan.FromHours(6), $"tentativa {tentativa} devolveu {atraso}");
        }
    }

    /// <summary>
    /// A janela util somada das 8 tentativas fica em torno de 24 h — tempo suficiente para
    /// o lojista recarregar a carteira antes de o envio ser dado como perdido.
    /// </summary>
    [Fact]
    public void Atraso_SomaDasOitoTentativas_FicaEmTornoDeVinteEQuatroHoras()
    {
        var total = TimeSpan.Zero;
        for (var tentativa = 0; tentativa < EnvioRetryPolicy.MaximoTentativas; tentativa++)
            total += EnvioRetryPolicy.Atraso(tentativa);

        // 2 + 4 + 8 + 16 + 32 + 64 + 128 + 256 = 510 min = 8 h 30 min de espera acumulada
        // ate a ultima tentativa disparar.
        Assert.Equal(TimeSpan.FromMinutes(510), total);
        Assert.True(total > TimeSpan.FromHours(8));
        Assert.True(total < TimeSpan.FromHours(24));
    }

    // ---------------------------------------------------------------- ProximaTentativa

    [Fact]
    public void ProximaTentativa_TentativaUm_SomaQuatroMinutosAoInstanteInformado()
    {
        var agora = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        var proxima = EnvioRetryPolicy.ProximaTentativa(agora, 1);

        Assert.Equal(new DateTime(2026, 3, 1, 10, 4, 0, DateTimeKind.Utc), proxima);
    }

    [Fact]
    public void ProximaTentativa_TentativaNoTeto_SomaSeisHoras()
    {
        var agora = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        var proxima = EnvioRetryPolicy.ProximaTentativa(agora, 12);

        Assert.Equal(new DateTime(2026, 3, 1, 16, 0, 0, DateTimeKind.Utc), proxima);
    }

    [Fact]
    public void ProximaTentativa_QualquerTentativa_EhSempreNoFuturoEPreservaOKind()
    {
        var agora = new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc);

        for (var tentativa = 0; tentativa <= 10; tentativa++)
        {
            var proxima = EnvioRetryPolicy.ProximaTentativa(agora, tentativa);
            Assert.True(proxima > agora, $"tentativa {tentativa} agendou para tras");
            Assert.Equal(DateTimeKind.Utc, proxima.Kind);
        }
    }

    /// <summary>Virada de ano: o Add do DateTime resolve, mas vale travar contra regressao de fuso.</summary>
    [Fact]
    public void ProximaTentativa_NaViradaDoAno_AtravessaADataCorretamente()
    {
        var agora = new DateTime(2026, 12, 31, 22, 0, 0, DateTimeKind.Utc);

        var proxima = EnvioRetryPolicy.ProximaTentativa(agora, 8); // teto de 6 h

        Assert.Equal(new DateTime(2027, 1, 1, 4, 0, 0, DateTimeKind.Utc), proxima);
    }

    // ---------------------------------------------------------------- EsgotouTentativas

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    public void EsgotouTentativas_AbaixoDoMaximo_RetornaFalso(int tentativas)
    {
        Assert.False(EnvioRetryPolicy.EsgotouTentativas(tentativas));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void EsgotouTentativas_NoMaximoOuAcima_RetornaVerdadeiro(int tentativas)
    {
        Assert.True(EnvioRetryPolicy.EsgotouTentativas(tentativas));
    }

    /// <summary>A fronteira exata: 7 ainda tenta, 8 desiste. Off-by-one aqui perde ou repete envio.</summary>
    [Fact]
    public void EsgotouTentativas_FronteiraDeSeteParaOito_MudaExatamenteNoMaximo()
    {
        Assert.False(EnvioRetryPolicy.EsgotouTentativas(EnvioRetryPolicy.MaximoTentativas - 1));
        Assert.True(EnvioRetryPolicy.EsgotouTentativas(EnvioRetryPolicy.MaximoTentativas));
    }

    [Fact]
    public void EsgotouTentativas_ContadorNegativo_RetornaFalso()
    {
        Assert.False(EnvioRetryPolicy.EsgotouTentativas(-1));
    }
}
