using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// Funcoes puras de CPF/CNPJ. O CPF do destinatario e obrigatorio no checkout: se ele passar
/// invalido, a etiqueta da transportadora falha DEPOIS do cliente ja ter pago.
/// </summary>
public class DocumentoHelperTests
{
    // ---------------------------------------------------------------- CPF valido

    [Theory]
    [InlineData("52998224725")]
    [InlineData("11144477735")]
    [InlineData("12345678909")]
    public void CpfValido_CpfRealSemMascara_RetornaVerdadeiro(string cpf)
    {
        Assert.True(DocumentoHelper.CpfValido(cpf));
    }

    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("111.444.777-35")]
    [InlineData("123.456.789-09")]
    [InlineData("529 982 247 25")]
    [InlineData("  529.982.247-25  ")]
    [InlineData("529-982-247/25")]
    public void CpfValido_CpfRealComMascaraOuRuido_RetornaVerdadeiro(string cpf)
    {
        Assert.True(DocumentoHelper.CpfValido(cpf));
    }

    // ---------------------------------------------------------------- CPF invalido

    [Theory]
    [InlineData("52998224726")] // segundo digito verificador errado
    [InlineData("52998224735")] // primeiro digito verificador errado
    [InlineData("12345678900")]
    [InlineData("11144477736")]
    [InlineData("529.982.247-24")]
    public void CpfValido_DigitoVerificadorErrado_RetornaFalso(string cpf)
    {
        Assert.False(DocumentoHelper.CpfValido(cpf));
    }

    /// <summary>
    /// 11111111111 e 00000000000 PASSAM no calculo ingenuo do DV. A rejeicao vem da guarda
    /// explicita de sequencia repetida — sem ela, todo formulario aceitaria esse lixo.
    /// </summary>
    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("33333333333")]
    [InlineData("44444444444")]
    [InlineData("55555555555")]
    [InlineData("66666666666")]
    [InlineData("77777777777")]
    [InlineData("88888888888")]
    [InlineData("99999999999")]
    [InlineData("111.111.111-11")]
    public void CpfValido_SequenciaRepetida_RetornaFalso(string cpf)
    {
        Assert.False(DocumentoHelper.CpfValido(cpf));
    }

    [Theory]
    [InlineData("5299822472")]      // 10 digitos
    [InlineData("529982247255")]    // 12 digitos
    [InlineData("1")]
    [InlineData("abcdefghijk")]     // vira string vazia depois de SomenteDigitos
    [InlineData("529.982.247-2A")]  // letra no lugar do digito derruba o tamanho
    public void CpfValido_TamanhoErrado_RetornaFalso(string cpf)
    {
        Assert.False(DocumentoHelper.CpfValido(cpf));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CpfValido_NuloOuVazio_RetornaFalso(string? cpf)
    {
        Assert.False(DocumentoHelper.CpfValido(cpf));
    }

    // ---------------------------------------------------------------- CNPJ

    [Theory]
    [InlineData("11222333000181")]
    [InlineData("00000000000191")]
    [InlineData("11.222.333/0001-81")]
    [InlineData("00.000.000/0001-91")]
    [InlineData("  11.222.333/0001-81  ")]
    public void CnpjValido_CnpjRealComOuSemMascara_RetornaVerdadeiro(string cnpj)
    {
        Assert.True(DocumentoHelper.CnpjValido(cnpj));
    }

    [Theory]
    [InlineData("11222333000182")] // segundo DV errado
    [InlineData("11222333000191")] // primeiro DV errado
    [InlineData("11.222.333/0001-80")]
    public void CnpjValido_DigitoVerificadorErrado_RetornaFalso(string cnpj)
    {
        Assert.False(DocumentoHelper.CnpjValido(cnpj));
    }

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("11111111111111")]
    [InlineData("99999999999999")]
    public void CnpjValido_SequenciaRepetida_RetornaFalso(string cnpj)
    {
        Assert.False(DocumentoHelper.CnpjValido(cnpj));
    }

    [Theory]
    [InlineData("1122233300018")]    // 13 digitos
    [InlineData("112223330001811")]  // 15 digitos
    [InlineData("11222333")]
    [InlineData("52998224725")]      // CPF valido nao e CNPJ valido
    [InlineData("abcdefghijklmn")]
    public void CnpjValido_TamanhoErrado_RetornaFalso(string cnpj)
    {
        Assert.False(DocumentoHelper.CnpjValido(cnpj));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CnpjValido_NuloOuVazio_RetornaFalso(string? cnpj)
    {
        Assert.False(DocumentoHelper.CnpjValido(cnpj));
    }

    // ---------------------------------------------------------------- SomenteDigitos

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("abc123def", "123")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SomenteDigitos_QualquerEntrada_DevolveApenasDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, DocumentoHelper.SomenteDigitos(entrada));
    }

    // ---------------------------------------------------------------- Formatar

    [Theory]
    [InlineData("52998224725", "529.982.247-25")]
    [InlineData("529.982.247-25", "529.982.247-25")]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    [InlineData("11.222.333/0001-81", "11.222.333/0001-81")]
    public void Formatar_TamanhoDeCpfOuCnpj_AplicaMascara(string entrada, string esperado)
    {
        Assert.Equal(esperado, DocumentoHelper.Formatar(entrada));
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("5299822472", "5299822472")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Formatar_TamanhoQueNaoEhCpfNemCnpj_DevolveSomenteOsDigitos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, DocumentoHelper.Formatar(entrada));
    }

    /// <summary>Formatar nao valida: um CPF com DV errado ainda sai mascarado. Nao confundir os dois.</summary>
    [Fact]
    public void Formatar_CpfComDigitoErrado_AindaAplicaMascaraSemValidar()
    {
        Assert.Equal("529.982.247-26", DocumentoHelper.Formatar("52998224726"));
        Assert.False(DocumentoHelper.CpfValido("52998224726"));
    }
}
