using Glorific.Api.Configuration;
using Xunit;

namespace Glorific.Tests.Seguranca;

/// <summary>
/// Fronteira de CORS. Um falso positivo aqui e uma origem hostil lendo resposta autenticada
/// do navegador do cliente — dai a quantidade de caso de fronteira.
///
/// A configuracao usada na maioria dos testes imita a de producao:
/// dominio raiz exato, curinga do proprio dominio e um host com porta nao padrao.
/// </summary>
public class CorsOriginMatcherTests
{
    private static readonly string[] ConfiguracaoDeProducao =
    [
        "https://glorific.art",
        "https://*.glorific.art",
        "https://loja.glorific.art:8443"
    ];

    private static CorsOriginMatcher Producao(bool permitirLocalhost = false) =>
        new(ConfiguracaoDeProducao, permitirLocalhost);

    // ============================================================ aceita

    [Theory]
    [InlineData("https://glorific.art")]          // raiz exata
    [InlineData("https://glorific.art/")]         // barra final: AbsolutePath "/" continua valendo
    [InlineData("https://glorific.art:443")]      // porta explicita igual a default do https
    [InlineData("  https://glorific.art  ")]      // espaco em volta e aparado antes de interpretar
    [InlineData("https://GLORIFIC.ART")]          // host e case-insensitive
    [InlineData("https://www.glorific.art")]      // curinga, primeiro rotulo
    [InlineData("https://loja.glorific.art")]     // curinga na porta default
    [InlineData("https://WWW.GLORIFIC.ART")]      // curinga tambem e case-insensitive
    [InlineData("https://preview-42.glorific.art")]
    [InlineData("https://loja.glorific.art:8443")] // entrada exata com porta nao padrao
    public void Corresponde_OrigemLiberada_RetornaVerdadeiro(string origem)
    {
        Assert.True(Producao().Corresponde(origem));
    }

    // ============================================================ ataques classicos

    /// <summary>
    /// O ataque que um EndsWith("glorific.art") deixaria passar. O ponto separador e
    /// obrigatorio, entao "evilglorific.art" NAO casa com "*.glorific.art".
    /// </summary>
    [Theory]
    [InlineData("https://evilglorific.art")]
    [InlineData("https://xglorific.art")]
    [InlineData("https://not-glorific.art")]
    [InlineData("https://myglorific.art")]
    public void Corresponde_HostQueApenasTerminaComONomeDoDominio_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>O dominio verdadeiro como PREFIXO de um dominio do atacante.</summary>
    [Theory]
    [InlineData("https://glorific.art.evil.com")]
    [InlineData("https://www.glorific.art.evil.com")]
    [InlineData("https://glorific.art.br")]
    [InlineData("https://glorificart.com")]
    public void Corresponde_DominioVerdadeiroComoPrefixoDeOutro_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>http e https sao origens DIFERENTES para o navegador. Liberar uma nao libera a outra.</summary>
    [Theory]
    [InlineData("http://glorific.art")]
    [InlineData("http://www.glorific.art")]
    [InlineData("http://loja.glorific.art:8443")]
    public void Corresponde_MesmoHostComEsquemaDiferente_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>Porta faz parte da origem: liberar :443 nao pode liberar :8443 nem :80.</summary>
    [Theory]
    [InlineData("https://glorific.art:8443")]
    [InlineData("https://glorific.art:8080")]
    [InlineData("https://www.glorific.art:8443")]
    [InlineData("https://loja.glorific.art:9443")]
    public void Corresponde_MesmoHostComPortaDiferente_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>Esquema fora de http/https nem chega a ser comparado com a lista.</summary>
    [Theory]
    [InlineData("ftp://glorific.art")]
    [InlineData("file://glorific.art")]
    [InlineData("chrome-extension://glorific.art")]
    [InlineData("data:text/html,<script>1</script>")]
    [InlineData("javascript:alert(1)")]
    public void Corresponde_EsquemaNaoSuportado_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>
    /// Origem de navegador e sempre scheme://host[:porta]. Caminho, query e userinfo sao as
    /// variacoes classicas usadas para confundir matcher escrito na base do StartsWith.
    /// </summary>
    [Theory]
    [InlineData("https://glorific.art/admin")]
    [InlineData("https://glorific.art/../evil")]
    [InlineData("https://glorific.art?x=1")]
    [InlineData("https://glorific.art/?x=1")]
    [InlineData("https://evil.com@glorific.art")]
    [InlineData("https://glorific.art@evil.com")]
    public void Corresponde_OrigemComCaminhoQueryOuUserInfo_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    /// <summary>
    /// "null" e literalmente o que o navegador manda em iframe sandbox, redirect opaco e
    /// pagina file://. Nao pode virar coringa.
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("*")]
    [InlineData("glorific.art")]        // sem esquema nao e origem
    [InlineData("//glorific.art")]
    [InlineData("https://")]
    [InlineData("nao e uma url")]
    [InlineData("https://[")]
    public void Corresponde_OrigemMalformada_RetornaFalso(string origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Corresponde_OrigemNulaOuVazia_RetornaFalso(string? origem)
    {
        Assert.False(Producao().Corresponde(origem));
    }

    // ============================================================ comportamentos que valem registrar

    /// <summary>
    /// O curinga NAO se limita a um rotulo: "a.b.glorific.art" tambem casa com "*.glorific.art",
    /// porque a regra e "termina em .glorific.art e e maior que isso". Comportamento real,
    /// travado aqui para que qualquer mudanca de politica apareca como quebra de teste.
    /// </summary>
    [Theory]
    [InlineData("https://a.b.glorific.art")]
    [InlineData("https://deploy.preview.loja.glorific.art")]
    public void Corresponde_SubdominioDeSegundoNivel_TambemCasaComOCuringa(string origem)
    {
        Assert.True(Producao().Corresponde(origem));
    }

    /// <summary>O proprio host base nao casa com o curinga; quem libera a raiz e a entrada exata.</summary>
    [Fact]
    public void Corresponde_HostBaseSemSubdominio_SoPassaPelaEntradaExata()
    {
        var somenteCuringa = new CorsOriginMatcher(["https://*.glorific.art"], permitirLocalhost: false);

        Assert.False(somenteCuringa.Corresponde("https://glorific.art"));
        Assert.True(somenteCuringa.Corresponde("https://www.glorific.art"));
    }

    /// <summary>
    /// Fragmento nao e checado (o navegador nunca manda um no header Origin), entao ele passa.
    /// Registrado para nao ser confundido com uma brecha nova caso alguem tope com isso.
    /// </summary>
    [Fact]
    public void Corresponde_OrigemComFragmento_Aceita_PoisFragmentoNaoEhAvaliado()
    {
        Assert.True(Producao().Corresponde("https://glorific.art#frag"));
    }

    // ============================================================ localhost

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://localhost:7443")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://[::1]:5173")]
    [InlineData("http://localhost")]
    public void Corresponde_ComLocalhostLiberado_AceitaQualquerPortaDeLoopback(string origem)
    {
        Assert.True(Producao(permitirLocalhost: true).Corresponde(origem));
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://[::1]:5173")]
    public void Corresponde_ComLocalhostBloqueado_RecusaLoopback(string origem)
    {
        Assert.False(Producao(permitirLocalhost: false).Corresponde(origem));
    }

    /// <summary>
    /// O ataque que um StartsWith("localhost") deixaria passar: dominio publico que apenas
    /// COMECA com localhost. IsLoopback resolve de verdade e devolve falso.
    /// </summary>
    [Theory]
    [InlineData("http://localhost.atacante.com")]
    [InlineData("http://localhost.evil.com:5173")]
    [InlineData("http://notlocalhost")]
    [InlineData("http://127.0.0.1.evil.com")]
    public void Corresponde_HostQueSoParecerLocalhost_RetornaFalso(string origem)
    {
        Assert.False(Producao(permitirLocalhost: true).Corresponde(origem));
    }

    /// <summary>Loopback liberado nao afrouxa a validacao de esquema.</summary>
    [Fact]
    public void Corresponde_LoopbackComEsquemaNaoSuportado_RetornaFalso()
    {
        Assert.False(Producao(permitirLocalhost: true).Corresponde("ftp://localhost:21"));
        Assert.False(Producao(permitirLocalhost: true).Corresponde("file://localhost/etc/passwd"));
    }

    // ============================================================ interpretacao da configuracao

    [Fact]
    public void Construtor_ConfiguracaoDeProducao_SeparaExatasDeCuringas()
    {
        var matcher = Producao();

        Assert.Equal(new[] { "https://glorific.art", "https://loja.glorific.art:8443" }, matcher.OrigensExatas);
        Assert.Equal(new[] { "https://*.glorific.art" }, matcher.OrigensCuringa);
        Assert.Empty(matcher.EntradasInvalidas);
    }

    /// <summary>
    /// Curinga em sufixo publico liberaria a internet inteira daquele TLD. Precisa de ponto
    /// no host base, e o "*" so vale no primeiro rotulo.
    /// </summary>
    [Theory]
    [InlineData("https://*.art")]
    [InlineData("https://*.com")]
    [InlineData("https://*.*.art")]
    [InlineData("https://*.*.glorific.art")]
    [InlineData("https://*")]
    [InlineData("https://*.")]
    [InlineData("https://gl*rific.art")]
    [InlineData("https://glorific.*")]
    public void Construtor_CuringaPerigosoOuMalPosicionado_VaiParaEntradasInvalidas(string entrada)
    {
        var matcher = new CorsOriginMatcher([entrada], permitirLocalhost: false);

        Assert.Equal(new[] { entrada }, matcher.EntradasInvalidas);
        Assert.Empty(matcher.OrigensExatas);
        Assert.Empty(matcher.OrigensCuringa);
    }

    [Theory]
    [InlineData("glorific.art")]                 // sem esquema
    [InlineData("://glorific.art")]              // esquema vazio
    [InlineData("ftp://glorific.art")]           // esquema nao suportado
    [InlineData("ws://glorific.art")]
    [InlineData("https://glorific.art/admin")]   // caminho na configuracao
    [InlineData("https://usuario@glorific.art")] // userinfo na configuracao
    [InlineData("lixo")]
    public void Construtor_EntradaQueNaoEhOrigem_VaiParaEntradasInvalidas(string entrada)
    {
        var matcher = new CorsOriginMatcher([entrada], permitirLocalhost: false);

        Assert.Equal(new[] { entrada }, matcher.EntradasInvalidas);
        Assert.False(matcher.TemAlgumaOrigem);
    }

    /// <summary>Entrada em branco e ignorada de vez: nao vira origem nem entra em invalidas.</summary>
    [Fact]
    public void Construtor_EntradasEmBranco_SaoIgnoradasSemPoluirALista()
    {
        var matcher = new CorsOriginMatcher(["", "   ", null!, "https://glorific.art"], permitirLocalhost: false);

        Assert.Empty(matcher.EntradasInvalidas);
        Assert.Equal(new[] { "https://glorific.art" }, matcher.OrigensExatas);
    }

    [Fact]
    public void Construtor_BarraFinalNaConfiguracao_EhAparadaEAOrigemContinuaValendo()
    {
        var matcher = new CorsOriginMatcher(["https://glorific.art/"], permitirLocalhost: false);

        Assert.Empty(matcher.EntradasInvalidas);
        Assert.True(matcher.Corresponde("https://glorific.art"));
    }

    [Fact]
    public void Construtor_EsquemaEHostEmMaiusculaNaConfiguracao_NormalizaECasa()
    {
        var matcher = new CorsOriginMatcher(["HTTPS://GLORIFIC.ART"], permitirLocalhost: false);

        Assert.Empty(matcher.EntradasInvalidas);
        Assert.True(matcher.Corresponde("https://glorific.art"));
        Assert.False(matcher.Corresponde("http://glorific.art"));
    }

    [Fact]
    public void Construtor_CuringaComPortaNaoPadrao_SoCasaNaquelaPorta()
    {
        var matcher = new CorsOriginMatcher(["https://*.glorific.art:8443"], permitirLocalhost: false);

        Assert.Equal(new[] { "https://*.glorific.art:8443" }, matcher.OrigensCuringa);
        Assert.True(matcher.Corresponde("https://loja.glorific.art:8443"));
        Assert.False(matcher.Corresponde("https://loja.glorific.art"));
        Assert.False(matcher.Corresponde("https://loja.glorific.art:443"));
    }

    [Fact]
    public void Construtor_UmaEntradaInvalidaNoMeio_NaoDerrubaAsValidas()
    {
        var matcher = new CorsOriginMatcher(
            ["https://glorific.art", "https://*.art", "https://*.glorific.art"],
            permitirLocalhost: false);

        Assert.Equal(new[] { "https://*.art" }, matcher.EntradasInvalidas);
        Assert.True(matcher.Corresponde("https://glorific.art"));
        Assert.True(matcher.Corresponde("https://www.glorific.art"));
        Assert.False(matcher.Corresponde("https://qualquer.art"));
    }

    // ============================================================ TemAlgumaOrigem

    [Fact]
    public void TemAlgumaOrigem_SemConfiguracaoESemLocalhost_EhFalso_CorsTotalmenteFechado()
    {
        var matcher = new CorsOriginMatcher([], permitirLocalhost: false);

        Assert.False(matcher.TemAlgumaOrigem);
        Assert.False(matcher.Corresponde("https://glorific.art"));
        Assert.False(matcher.Corresponde("http://localhost:5173"));
    }

    [Fact]
    public void TemAlgumaOrigem_SemConfiguracaoMasComLocalhost_EhVerdadeiro()
    {
        var matcher = new CorsOriginMatcher([], permitirLocalhost: true);

        Assert.True(matcher.TemAlgumaOrigem);
        Assert.True(matcher.Corresponde("http://localhost:5173"));
        Assert.False(matcher.Corresponde("https://glorific.art"));
    }

    [Fact]
    public void TemAlgumaOrigem_SoEntradasInvalidas_EhFalso()
    {
        var matcher = new CorsOriginMatcher(["https://*.com", "lixo"], permitirLocalhost: false);

        Assert.False(matcher.TemAlgumaOrigem);
        Assert.Equal(2, matcher.EntradasInvalidas.Count);
    }

    [Fact]
    public void Construtor_ColecaoNula_NaoLancaEDeixaOCorsFechado()
    {
        var matcher = new CorsOriginMatcher(null, permitirLocalhost: false);

        Assert.False(matcher.TemAlgumaOrigem);
        Assert.Empty(matcher.OrigensExatas);
        Assert.Empty(matcher.OrigensCuringa);
        Assert.Empty(matcher.EntradasInvalidas);
        Assert.False(matcher.Corresponde("https://glorific.art"));
    }

    // ============================================================ determinismo

    [Fact]
    public void Corresponde_ChamadoVariasVezes_DevolveSempreOMesmoResultado()
    {
        var matcher = Producao(permitirLocalhost: true);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(matcher.Corresponde("https://www.glorific.art"));
            Assert.False(matcher.Corresponde("https://evilglorific.art"));
            Assert.True(matcher.Corresponde("http://localhost:5173"));
        }
    }
}
