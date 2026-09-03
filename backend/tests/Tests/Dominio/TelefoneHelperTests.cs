using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// Telefone do destinatario: a transportadora liga para ele. Fixo com 10 digitos e celular
/// com 11 (obrigatoriamente com o 9 depois do DDD).
/// </summary>
public class TelefoneHelperTests
{
    [Theory]
    [InlineData("11987654321")]  // celular SP
    [InlineData("41991234567")]  // celular PR
    [InlineData("99987654321")]  // maior DDD possivel
    [InlineData("11912345678")]
    public void Valido_CelularComDddEComNove_RetornaVerdadeiro(string telefone)
    {
        Assert.True(TelefoneHelper.Valido(telefone));
    }

    [Theory]
    [InlineData("1133334444")]
    [InlineData("4133334444")]
    [InlineData("1130000000")]
    public void Valido_FixoComDezDigitos_RetornaVerdadeiro(string telefone)
    {
        Assert.True(TelefoneHelper.Valido(telefone));
    }

    /// <summary>
    /// Numero em formato internacional tem 13 digitos e NAO passa: o helper so entende
    /// DDD nacional. Vale registrar porque o campo do checkout aceita colar do WhatsApp.
    /// </summary>
    [Fact]
    public void Valido_CelularComCodigoDoPais_RetornaFalso()
    {
        Assert.False(TelefoneHelper.Valido("+55 11 98765-4321"));
        Assert.True(TelefoneHelper.Valido("(11) 98765-4321"));
    }

    [Theory]
    [InlineData("(11) 98765-4321")]
    [InlineData("11 98765 4321")]
    [InlineData("11.98765.4321")]
    public void Valido_CelularComMascaraVariada_RetornaVerdadeiro(string telefone)
    {
        Assert.True(TelefoneHelper.Valido(telefone));
    }

    /// <summary>Celular no Brasil comeca com 9 depois do DDD. Sem o 9, o numero e legado e invalido.</summary>
    [Theory]
    [InlineData("11887654321")]
    [InlineData("11787654321")]
    [InlineData("11087654321")]
    [InlineData("(11) 88765-4321")]
    public void Valido_CelularSemONoveAposODdd_RetornaFalso(string telefone)
    {
        Assert.False(TelefoneHelper.Valido(telefone));
    }

    [Theory]
    [InlineData("00987654321")]
    [InlineData("01987654321")]
    [InlineData("09987654321")]
    [InlineData("10987654321")]
    [InlineData("0133334444")]
    [InlineData("1033334444")]
    public void Valido_DddForaDaFaixa_RetornaFalso(string telefone)
    {
        Assert.False(TelefoneHelper.Valido(telefone));
    }

    [Theory]
    [InlineData("987654321")]      // 9 digitos, sem DDD
    [InlineData("119876543210")]   // 12 digitos
    [InlineData("5511987654321")]  // 13 digitos, com codigo do pais
    [InlineData("119")]
    [InlineData("abcdefghij")]     // sem digito nenhum
    public void Valido_QuantidadeDeDigitosForaDeDezOuOnze_RetornaFalso(string telefone)
    {
        Assert.False(TelefoneHelper.Valido(telefone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Valido_NuloOuVazio_RetornaFalso(string? telefone)
    {
        Assert.False(TelefoneHelper.Valido(telefone));
    }

    [Theory]
    [InlineData("11987654321", "(11) 98765-4321")]
    [InlineData("(11) 98765-4321", "(11) 98765-4321")]
    [InlineData("1133334444", "(11) 3333-4444")]
    [InlineData("11 3333 4444", "(11) 3333-4444")]
    public void Formatar_DezOuOnzeDigitos_AplicaMascaraCorreta(string entrada, string esperado)
    {
        Assert.Equal(esperado, TelefoneHelper.Formatar(entrada));
    }

    [Theory]
    [InlineData("987654321", "987654321")]
    [InlineData("5511987654321", "5511987654321")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Formatar_TamanhoInesperado_DevolveSomenteOsDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, TelefoneHelper.Formatar(entrada));
    }

    /// <summary>Formatar nao valida: DDD invalido continua saindo mascarado.</summary>
    [Fact]
    public void Formatar_DddInvalido_AindaAplicaMascaraSemValidar()
    {
        Assert.Equal("(10) 98765-4321", TelefoneHelper.Formatar("10987654321"));
        Assert.False(TelefoneHelper.Valido("10987654321"));
    }

    [Theory]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("abc", "")]
    [InlineData(null, "")]
    public void SomenteDigitos_QualquerEntrada_DevolveApenasDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, TelefoneHelper.SomenteDigitos(entrada));
    }
}
