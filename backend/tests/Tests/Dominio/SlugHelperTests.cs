using Glorific.Domain.Helpers;
using Xunit;

namespace Glorific.Tests.Dominio;

/// <summary>
/// Slug de catalogo e SEO-critico e vira URL publica permanente. Regressao aqui muda a URL
/// de um produto ja indexado.
/// </summary>
public class SlugHelperTests
{
    [Theory]
    [InlineData("Vestido Túnica", "vestido-tunica")]
    [InlineData("Coração de Açúcar", "coracao-de-acucar")]
    [InlineData("São Paulo", "sao-paulo")]
    [InlineData("Camisa Polo Jacquard Ácido", "camisa-polo-jacquard-acido")]
    [InlineData("Blusa Crème", "blusa-creme")]
    public void Gerar_TextoComAcentoOuCedilha_RemoveDiacriticoMantendoALetraBase(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    [Theory]
    [InlineData("VESTIDO MIDI", "vestido-midi")]
    [InlineData("VESTIDO MÍDI", "vestido-midi")]
    [InlineData("Vestido Midi", "vestido-midi")]
    [InlineData("vEsTiDo MiDi", "vestido-midi")]
    public void Gerar_VariacoesDeCaixa_ProduzemOMesmoSlug(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    [Theory]
    [InlineData("vestido  linho", "vestido-linho")]
    [InlineData("vestido   de    linho", "vestido-de-linho")]
    [InlineData("  vestido linho  ", "vestido-linho")]
    [InlineData("vestido\tlinho", "vestido-linho")]
    [InlineData("vestido\nlinho", "vestido-linho")]
    public void Gerar_EspacoDuplicadoOuNasBordas_ColapsaEmUmHifenSo(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    [Theory]
    [InlineData("Vestido & Saia!", "vestido-saia")]
    [InlineData("Camiseta 100% Algodão", "camiseta-100-algodao")]
    [InlineData("Bolsa (Edição Limitada)", "bolsa-edicao-limitada")]
    [InlineData("Anel #1 — Prata", "anel-1-prata")]
    [InlineData("R$ 199,90", "r-19990")]
    public void Gerar_TextoComSimbolo_DescartaOSimbolo(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    /// <summary>
    /// A barra e removida SEM virar separador: "Verão/Inverno" vira "veraoinverno", nao
    /// "verao-inverno". Comportamento real do helper, registrado para nao mudar por acidente.
    /// </summary>
    [Theory]
    [InlineData("Verão/Inverno 2026", "veraoinverno-2026")]
    [InlineData("Preto/Branco", "pretobranco")]
    public void Gerar_TextoComBarra_JuntaAsPalavrasSemSeparador(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    [Theory]
    [InlineData("Off-White", "off-white")]
    [InlineData("-- vestido --", "vestido")]
    [InlineData("---", "")]
    [InlineData("vestido---linho", "vestido-linho")]
    [InlineData("- vestido - linho -", "vestido-linho")]
    public void Gerar_HifenNasBordasOuRepetido_NormalizaParaUmHifenInterno(string texto, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.Gerar(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData("!!!")]
    [InlineData("日本語")]
    public void Gerar_EntradaSemNenhumCaractereAproveitavel_RetornaStringVazia(string? texto)
    {
        Assert.Equal(string.Empty, SlugHelper.Gerar(texto));
    }

    [Fact]
    public void Gerar_TextoJaEmFormatoDeSlug_EhIdempotente()
    {
        var slug = SlugHelper.Gerar("Vestido Midi de Linho Off-White");
        Assert.Equal("vestido-midi-de-linho-off-white", slug);
        Assert.Equal(slug, SlugHelper.Gerar(slug));
    }

    [Fact]
    public void Gerar_QualquerTexto_NaoProduzHifenNasBordas()
    {
        foreach (var texto in new[] { "- a -", "  ÁGUA  ", "***b***", "--", "1" })
        {
            var slug = SlugHelper.Gerar(texto);
            Assert.False(slug.StartsWith('-'), $"slug '{slug}' comeca com hifen");
            Assert.False(slug.EndsWith('-'), $"slug '{slug}' termina com hifen");
        }
    }

    // ---------------------------------------------------------------- ComSufixo

    [Theory]
    [InlineData(2, "vestido-linho-2")]
    [InlineData(3, "vestido-linho-3")]
    [InlineData(10, "vestido-linho-10")]
    [InlineData(999, "vestido-linho-999")]
    public void ComSufixo_SufixoMaiorQueUm_AcrescentaONumero(int sufixo, string esperado)
    {
        Assert.Equal(esperado, SlugHelper.ComSufixo("vestido-linho", sufixo));
    }

    /// <summary>O primeiro produto com aquele nome fica com o slug limpo, sem "-1".</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ComSufixo_SufixoUmOuMenor_DevolveOSlugIntacto(int sufixo)
    {
        Assert.Equal("vestido-linho", SlugHelper.ComSufixo("vestido-linho", sufixo));
    }

    [Fact]
    public void ComSufixo_SlugVazio_DevolveApenasOSufixo()
    {
        Assert.Equal("-2", SlugHelper.ComSufixo(string.Empty, 2));
        Assert.Equal(string.Empty, SlugHelper.ComSufixo(string.Empty, 1));
    }
}
