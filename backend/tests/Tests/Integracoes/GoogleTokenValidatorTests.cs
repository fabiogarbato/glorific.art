using System.Text;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports.Options;
using Glorific.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glorific.Tests.Integracoes;

/// <summary>
/// Testes do adaptador do login com Google.
///
/// Sobre REDE: nenhum teste aqui chega ao JWKS do Google. Todos os tokens usados sao recusados
/// pelas guardas BARATAS da validacao — estrutura do JWT, algoritmo, issuer, audience e
/// expiracao — que rodam antes de qualquer conferencia de assinatura. Um token que passasse por
/// todas elas exigiria a chave privada do Google, o que nao existe em teste.
///
/// O contrato coberto e o da porta: token invalido devolve null (o servico traduz para 401) e
/// NAO lanca; erro de CONFIGURACAO, ao contrario, sobe como excecao — sao problemas diferentes e
/// nao podem virar a mesma resposta para o cliente.
/// </summary>
public sealed class GoogleTokenValidatorTests
{
    private const string NossoClientId = "111111111111-glorific.apps.googleusercontent.com";
    private const string OutroClientId = "999999999999-outro-app.apps.googleusercontent.com";

    private const string EmissorDoGoogle = "https://accounts.google.com";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidarAsync_ComTokenVazio_DevolveNullSemLancar(string? idToken)
    {
        var validador = Criar();

        var identidade = await validador.ValidarAsync(idToken!);

        Assert.Null(identidade);
    }

    [Theory]
    [InlineData("nao-e-um-jwt")]
    [InlineData("somente.duas")]
    [InlineData("segmentos.demais.para.um.jwt")]
    [InlineData("...")]
    [InlineData("!!!.???.###")]
    public async Task ValidarAsync_ComTokenMalformado_DevolveNullSemLancar(string idToken)
    {
        var validador = Criar();

        // Token quebrado e caso ESPERADO no endpoint de login (gente com sessao velha, front
        // desatualizado, alguem testando a rota na mao). Nao pode virar 500.
        var identidade = await validador.ValidarAsync(idToken);

        Assert.Null(identidade);
    }

    [Fact]
    public async Task ValidarAsync_ComAlgoritmoNone_DevolveNull()
    {
        // Downgrade classico: "alg":"none" com assinatura vazia. Tem que morrer na entrada.
        var token = MontarIdToken(
            algoritmo: "none",
            emissor: EmissorDoGoogle,
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        var identidade = await Criar().ValidarAsync(token);

        Assert.Null(identidade);
    }

    [Fact]
    public async Task ValidarAsync_ComEmissorDesconhecido_DevolveNull()
    {
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: "https://accounts.google.com.atacante.test",
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        var identidade = await Criar().ValidarAsync(token);

        Assert.Null(identidade);
    }

    [Fact]
    public async Task ValidarAsync_ComAudienceDeOutroAplicativo_DevolveNull()
    {
        // O ponto que costuma ser esquecido: sem conferir aud, um id_token emitido para QUALQUER
        // outro aplicativo Google — e obter um desses e trivial — passaria como login valido aqui.
        // O token abaixo esta dentro da validade e tem issuer legitimo: a UNICA coisa errada e a
        // audience.
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: EmissorDoGoogle,
            audience: OutroClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        var identidade = await Criar().ValidarAsync(token);

        Assert.Null(identidade);
    }

    [Fact]
    public async Task ValidarAsync_ComTokenExpirado_DevolveNull()
    {
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: EmissorDoGoogle,
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(-2));

        var identidade = await Criar().ValidarAsync(token);

        Assert.Null(identidade);
    }

    // ------------------------------------------------------------------
    // (h) Configuracao ausente
    // ------------------------------------------------------------------

    [Theory]
    // Em branco.
    [InlineData("")]
    [InlineData("   ")]
    // O placeholder do appsettings versionado. So conferir "vazio" o deixaria passar batido, e
    // ele viraria uma audience que nenhum id_token real casa — a loja passaria a responder
    // "login invalido" para TODO mundo, com a causa real escondida.
    [InlineData("!!NO_KEY_PROVIDED!!")]
    // Texto de "preencha aqui" e variavel trocada: nao terminam no sufixo do Google, entao nao
    // sao client id nenhum.
    [InlineData("defina-Google__ClientId-para-testar-login-google")]
    [InlineData("glorific.art")]
    public async Task ValidarAsync_SemClientIdConfigurado_LancaErroDeConfiguracaoEmVezDeDevolverNull(string clientId)
    {
        var validador = Criar(new GoogleOptions { ClientId = clientId });

        // Configuracao ausente NAO pode virar "token invalido": isso apareceria como 401 e
        // mandaria todo mundo procurar bug no front.
        var excecao = await Assert.ThrowsAsync<IntegracaoNaoConfiguradaException>(
            () => validador.ValidarAsync("qualquer.coisa.aqui"));

        // Mensagem TECNICA, com o nome exato da chave: e ela que vai para o log.
        Assert.Contains("Google:ClientId", excecao.Message, StringComparison.Ordinal);
        Assert.Equal("Google", excecao.Integracao);

        // Mensagem PUBLICA, a unica que chega ao navegador: diz o que houve e nao expoe o nome
        // da variavel de ambiente.
        Assert.Equal(GoogleTokenValidator.MensagemNaoConfigurado, excecao.MensagemPublica);
        Assert.DoesNotContain("Google__ClientId", excecao.MensagemPublica, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidarAsync_SemClientIdConfigurado_FalhaAntesDeOlharOToken()
    {
        var validador = Criar(new GoogleOptions { ClientId = string.Empty });

        // Nem token vazio escapa: com a loja desconfigurada o motivo da falha e sempre esse, e
        // devolver null aqui viraria um 401 que culpa a credencial do cliente.
        await Assert.ThrowsAsync<IntegracaoNaoConfiguradaException>(() => validador.ValidarAsync(string.Empty));
    }

    // ------------------------------------------------------------------
    // (b) Emissor aceito: accounts.google.com e https://accounts.google.com
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("accounts.google.com")]
    [InlineData("https://accounts.google.com")]
    public void EmissorAceito_ComOsDoisEmissoresDoGoogle_Aceita(string emissor)
    {
        // Os dois convivem em tokens reais. Aceitar so o com esquema recusaria login legitimo;
        // e um teste que so olhasse um dos dois nao perceberia.
        Assert.True(GoogleIdTokenGuardas.EmissorAceito(emissor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("accounts.google.com.atacante.test")]
    [InlineData("https://accounts.google.com.atacante.test")]
    [InlineData("https://accounts.google.com/")]
    [InlineData("http://accounts.google.com")]
    [InlineData("ACCOUNTS.GOOGLE.COM")]
    [InlineData("https://login.microsoftonline.com")]
    public void EmissorAceito_ComQualquerOutroEmissor_Recusa(string? emissor)
    {
        // Comparacao exata de proposito: "iss" e identificador. Aceitar por sufixo deixaria
        // passar accounts.google.com.atacante.test, que e um dominio de terceiro.
        Assert.False(GoogleIdTokenGuardas.EmissorAceito(emissor));
    }

    [Fact]
    public async Task ValidarAsync_ComEmissorDeDominioParecido_DevolveNull()
    {
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: "https://accounts.google.com.atacante.test",
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        Assert.Null(await Criar().ValidarAsync(token));
    }

    // ------------------------------------------------------------------
    // (a) Audience conferida contra o NOSSO Google:ClientId
    // ------------------------------------------------------------------

    [Fact]
    public void PodeSeguirParaValidacao_ComAudienceDeOutroAplicativo_Reprova()
    {
        // A falha classica que permite entrar como qualquer pessoa: o token e legitimo, assinado
        // pelo Google, dentro da validade e com issuer certo. A UNICA coisa errada e o aud — e
        // conseguir um id_token emitido para outro aplicativo Google e trivial.
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: EmissorDoGoogle,
            audience: OutroClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        Assert.False(GoogleIdTokenGuardas.PodeSeguirParaValidacao(token, NossoClientId, out var motivo));
        Assert.Contains("aud", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PodeSeguirParaValidacao_ComANossaAudience_Aprova()
    {
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: EmissorDoGoogle,
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        // "Aprova" aqui significa apenas "segue para a validacao de assinatura". Nada nesta
        // guarda prova que o Google assinou coisa alguma.
        Assert.True(GoogleIdTokenGuardas.PodeSeguirParaValidacao(token, NossoClientId, out _));
    }

    [Fact]
    public void PodeSeguirParaValidacao_ComEmissorErrado_Reprova()
    {
        var token = MontarIdToken(
            algoritmo: "RS256",
            emissor: "https://accounts.google.com.atacante.test",
            audience: NossoClientId,
            expiraEm: DateTimeOffset.UtcNow.AddHours(1));

        Assert.False(GoogleIdTokenGuardas.PodeSeguirParaValidacao(token, NossoClientId, out var motivo));
        Assert.Contains("iss", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AudienceAceita_ComAudComoArrayContendoONossoClientId_Aceita()
    {
        // O JWT permite aud como array. O Google emite string, mas fechar a porta para o array
        // recusaria um token legitimo no dia em que isso mudasse.
        Assert.True(GoogleIdTokenGuardas.AudienceAceita([OutroClientId, NossoClientId], NossoClientId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AudienceAceita_ComClientIdVazio_Recusa(string clientId)
    {
        // Sem ClientId nao existe audience esperada — e "qualquer audience serve" seria
        // exatamente a falha que esta guarda existe para impedir.
        Assert.False(GoogleIdTokenGuardas.AudienceAceita([clientId], clientId));
    }

    [Theory]
    [InlineData("nao-e-um-jwt")]
    [InlineData("uma.parte.so.demais.aqui")]
    [InlineData("!!!.???.###")]
    public void PodeSeguirParaValidacao_ComTokenIlegivel_DeixaABibliotecaDecidir(string idToken)
    {
        // A guarda so sabe REPROVAR o que ela conseguiu ler. Fechar aqui por falha de parse
        // duplicaria a decisao em dois lugares — e a biblioteca recusa isso de qualquer forma.
        Assert.True(GoogleIdTokenGuardas.PodeSeguirParaValidacao(idToken, NossoClientId, out _));
    }

    [Fact]
    public async Task ValidarAsync_ComCancelamentoSolicitado_PropagaOCancelamento()
    {
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var validador = Criar();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => validador.ValidarAsync("qualquer.coisa.aqui", cancelamento.Token));
    }

    [Fact]
    public async Task ValidarAsync_ComTokenVazioECancelamentoSolicitado_DevolveNullAntesDeChecarCancelamento()
    {
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var validador = Criar();

        // Documenta a ordem real das guardas: string vazia sai antes de qualquer outra coisa.
        Assert.Null(await validador.ValidarAsync(string.Empty, cancelamento.Token));
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static GoogleTokenValidator Criar(GoogleOptions? opcoes = null) =>
        new(
            new MonitorDeOpcoesGoogle(opcoes ?? new GoogleOptions
            {
                ClientId = NossoClientId,
                ToleranciaRelogioSegundos = 30
            }),
            NullLogger<GoogleTokenValidator>.Instance);

    /// <summary>
    /// Monta um id_token estruturalmente valido e assinado com lixo.
    ///
    /// A assinatura nunca chega a ser conferida nos casos usados aqui: algoritmo, issuer,
    /// audience e expiracao sao checados antes, sem rede.
    /// </summary>
    private static string MontarIdToken(
        string algoritmo,
        string emissor,
        string audience,
        DateTimeOffset expiraEm)
    {
        var cabecalho = $$"""{"alg":"{{algoritmo}}","kid":"chave-de-teste","typ":"JWT"}""";

        var corpo = $$"""
        {"iss":"{{emissor}}","azp":"{{audience}}","aud":"{{audience}}",
         "iat":{{expiraEm.AddHours(-1).ToUnixTimeSeconds()}},
         "exp":{{expiraEm.ToUnixTimeSeconds()}},
         "sub":"108120912834729387","email":"maria@exemplo.test",
         "email_verified":true,"name":"Maria Souza","picture":"https://exemplo.test/foto.png"}
        """;

        return string.Join(
            '.',
            Base64Url(cabecalho),
            Base64Url(corpo),
            Base64Url("assinatura-de-teste-que-nunca-sera-conferida"));
    }

    private static string Base64Url(string texto) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

/// <summary>
/// IOptionsMonitor a mao — o projeto nao usa biblioteca de mock. Devolve sempre o mesmo valor e
/// nunca notifica mudanca, que e exatamente o comportamento deterministico que o teste precisa.
/// </summary>
internal sealed class MonitorDeOpcoesGoogle(GoogleOptions opcoes) : IOptionsMonitor<GoogleOptions>
{
    public GoogleOptions CurrentValue { get; } = opcoes;

    public GoogleOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<GoogleOptions, string?> listener) => null;
}
