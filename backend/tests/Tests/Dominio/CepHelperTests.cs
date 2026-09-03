using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// CEP e a chave da cotacao de frete. CEP invalido chegando no Melhor Envio devolve erro
/// generico e o cliente ve "nao foi possivel calcular" sem saber o porque.
/// </summary>
public class CepHelperTests
{
    [Theory]
    [InlineData("01310100")]   // Av. Paulista
    [InlineData("80010000")]   // Curitiba centro
    [InlineData("20040020")]   // Rio centro
    [InlineData("99999999")]
    [InlineData("00000001")]   // extremo: nao e "tudo zero", entao passa
    [InlineData("10000000")]
    public void Valido_OitoDigitosSemMascara_RetornaVerdadeiro(string cep)
    {
        Assert.True(CepHelper.Valido(cep));
    }

    [Theory]
    [InlineData("01310-100")]
    [InlineData("01310 100")]
    [InlineData("  01310-100  ")]
    [InlineData("01.310-100")]
    public void Valido_OitoDigitosComMascara_RetornaVerdadeiro(string cep)
    {
        Assert.True(CepHelper.Valido(cep));
    }

    /// <summary>"00000000" tem o tamanho certo mas nao existe. Guarda explicita no helper.</summary>
    [Theory]
    [InlineData("00000000")]
    [InlineData("00000-000")]
    [InlineData("0000 0000")]
    public void Valido_TodosOsDigitosZerados_RetornaFalso(string cep)
    {
        Assert.False(CepHelper.Valido(cep));
    }

    [Theory]
    [InlineData("0131010")]     // 7 digitos
    [InlineData("013101000")]   // 9 digitos
    [InlineData("1")]
    [InlineData("0131-010")]
    [InlineData("abcdefgh")]    // nenhum digito
    [InlineData("01310-10A")]   // letra derruba para 7 digitos
    public void Valido_QuantidadeDeDigitosDiferenteDeOito_RetornaFalso(string cep)
    {
        Assert.False(CepHelper.Valido(cep));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Valido_NuloOuVazio_RetornaFalso(string? cep)
    {
        Assert.False(CepHelper.Valido(cep));
    }

    [Theory]
    [InlineData("01310100", "01310-100")]
    [InlineData("01310-100", "01310-100")]
    [InlineData("01310 100", "01310-100")]
    [InlineData("00000000", "00000-000")]
    public void Formatar_OitoDigitos_AplicaMascara(string entrada, string esperado)
    {
        Assert.Equal(esperado, CepHelper.Formatar(entrada));
    }

    [Theory]
    [InlineData("0131010", "0131010")]
    [InlineData("013101000", "013101000")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Formatar_TamanhoDiferenteDeOito_DevolveSomenteOsDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, CepHelper.Formatar(entrada));
    }

    /// <summary>Formatar nao valida: o CEP zerado sai mascarado mesmo sendo recusado por Valido.</summary>
    [Fact]
    public void Formatar_CepZerado_AindaAplicaMascaraSemValidar()
    {
        Assert.Equal("00000-000", CepHelper.Formatar("00000000"));
        Assert.False(CepHelper.Valido("00000000"));
    }

    [Theory]
    [InlineData("01310-100", "01310100")]
    [InlineData("abc", "")]
    [InlineData(null, "")]
    public void SomenteDigitos_QualquerEntrada_DevolveApenasDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, CepHelper.SomenteDigitos(entrada));
    }

    /// <summary>Formatar e idempotente: aplicar duas vezes nao duplica o hifen.</summary>
    [Fact]
    public void Formatar_AplicadoDuasVezes_ProduzOMesmoResultado()
    {
        var uma = CepHelper.Formatar("01310100");
        var duas = CepHelper.Formatar(uma);
        Assert.Equal(uma, duas);
    }
}
