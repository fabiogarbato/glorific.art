using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Application.Models.Auth;
using Glorific.Domain.Constants;
using Glorific.Infrastructure.Auth;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// O LOGIN COM GOOGLE, exercitado de ponta a ponta pela primeira vez.
///
/// Este fluxo existia inteiro — controller, servico, adaptador — e NUNCA tinha sido executado.
/// Codigo de autenticacao que nunca rodou e a pior combinacao possivel: parece pronto na
/// revisao, e o primeiro a rodar de verdade e quem estiver tentando entrar na conta de outra
/// pessoa.
///
/// A pergunta central de cada teste aqui e sempre a mesma: "isto deixa alguem entrar como quem
/// nao e?". As duas portas por onde isso passaria:
///   - audience: um id_token legitimo emitido para OUTRO aplicativo Google (coberto offline em
///     GoogleTokenValidatorTests, porque depende do adaptador e nao do banco);
///   - e-mail nao verificado: criar uma conta Google com o e-mail de outra pessoa, sem prova-lo,
///     e cair no casamento por e-mail. Este e o teste mais importante do arquivo.
///
/// A validacao criptografica em si esta dublada (ver GoogleDeTeste): so a chave privada do
/// Google produz um id_token que passa de verdade. O que esta sob teste aqui e o que a loja faz
/// DEPOIS de o Google dizer quem e a pessoa.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class LoginGoogleTests
{
    private readonly ApiFixture _api;

    public LoginGoogleTests(ApiFixture api)
    {
        _api = api;
    }

    // ==================================================================
    // (c) Casamento por e-mail SO com email_verified = true
    // ==================================================================

    /// <summary>
    /// O teste que impede tomada de conta alheia.
    ///
    /// Cenario do atacante: a Maria ja e cliente da loja com maria@exemplo. O atacante cria uma
    /// conta Google declarando maria@exemplo, o Google devolve email_verified = FALSE porque ele
    /// nunca provou nada, e o back — se casar por e-mail assim mesmo — entrega a conta da Maria.
    /// </summary>
    [Fact]
    public async Task LoginGoogle_ComEmailNaoVerificado_NaoVinculaAContaExistente()
    {
        var vitima = await _api.RegistrarClienteAsync();

        var idToken = _api.Google.RegistrarConta(vitima.Email, emailVerificado: false);

        using var cliente = _api.CriarClienteGoogle();
        using var resposta = await Entrar(cliente, idToken);

        Assert.False(
            resposta.IsSuccessStatusCode,
            "E-mail Google NAO verificado abriu sessao. Criar uma conta Google com o e-mail de " +
            "outra pessoa passaria a ser suficiente para assumir a conta dela na loja.");

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);
        Assert.Contains("verificado", envelope.Error, StringComparison.OrdinalIgnoreCase);

        // A conta da vitima nao pode ter sido tocada: sem vinculo Google, e a senha dela
        // continua sendo a unica porta de entrada.
        var perfil = await PerfilAdminAsync(vitima.Id);

        Assert.False(
            perfil.GetProperty("googleVinculado").GetBoolean(),
            "Um id_token com e-mail NAO verificado deixou vinculo gravado na conta da vitima.");

        Assert.True(perfil.GetProperty("temSenha").GetBoolean());
    }

    [Fact]
    public async Task LoginGoogle_ComEmailVerificado_VinculaAContaExistenteEMantemOMesmoUsuario()
    {
        var cliente = await _api.RegistrarClienteAsync();

        var idToken = _api.Google.RegistrarConta(cliente.Email);

        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, idToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var usuario = await UsuarioDaSessaoAsync(resposta);

        // MESMO usuario, nao um segundo cadastro com o mesmo e-mail.
        Assert.Equal(cliente.Uuid, usuario.GetProperty("uuid").GetString());
        Assert.True(usuario.GetProperty("googleVinculado").GetBoolean());

        // O Google acabou de provar a posse do endereco.
        Assert.True(usuario.GetProperty("emailVerificado").GetBoolean());

        // Quem ja tinha senha continua com ela: vincular Google nao pode derrubar a outra forma
        // de entrar, senao a pessoa perde o acesso se um dia desvincular.
        Assert.True(usuario.GetProperty("temSenha").GetBoolean());
    }

    // ==================================================================
    // (d) A identidade e o claim sub, nunca o e-mail
    // ==================================================================

    /// <summary>
    /// A pessoa troca o e-mail da conta Google dela. O sub NAO muda — e por ele que o vinculo e
    /// encontrado. Se a chave fosse o e-mail, ela cairia num cadastro novo e perderia pedidos,
    /// enderecos e lista de desejos.
    /// </summary>
    [Fact]
    public async Task LoginGoogle_ComOMesmoSubEEmailDiferente_ReconheceOMesmoUsuario()
    {
        var sub = $"sub-estavel-{Guid.NewGuid():N}";
        var emailAntigo = EmailNovo();
        var emailNovo = EmailNovo();

        using var http = _api.CriarClienteGoogle();

        using var primeira = await Entrar(http, _api.Google.RegistrarConta(emailAntigo, subject: sub));
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var uuid = (await UsuarioDaSessaoAsync(primeira)).GetProperty("uuid").GetString();

        using var segunda = await Entrar(http, _api.Google.RegistrarConta(emailNovo, subject: sub));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var depois = await UsuarioDaSessaoAsync(segunda);

        Assert.Equal(uuid, depois.GetProperty("uuid").GetString());
    }

    /// <summary>
    /// O outro lado da mesma regra: dois subs DIFERENTES nao podem ser tratados como a mesma
    /// pessoa so porque o e-mail bate por acaso — e nem podem virar dois cadastros com o mesmo
    /// e-mail, que o indice unico de usuarios recusaria com erro de banco.
    /// </summary>
    [Fact]
    public async Task LoginGoogle_ComSubDiferenteEMesmoEmailVerificado_CaiNaMesmaContaSemDuplicar()
    {
        var email = EmailNovo();

        using var http = _api.CriarClienteGoogle();

        using var primeira = await Entrar(http, _api.Google.RegistrarConta(email));
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var uuid = (await UsuarioDaSessaoAsync(primeira)).GetProperty("uuid").GetString();

        using var segunda = await Entrar(http, _api.Google.RegistrarConta(email));

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(uuid, (await UsuarioDaSessaoAsync(segunda)).GetProperty("uuid").GetString());
    }

    [Fact]
    public async Task LoginGoogle_ComSubVazio_Recusa()
    {
        var idToken = _api.Google.Registrar(new GoogleIdentityInfo
        {
            Subject = string.Empty,
            Email = EmailNovo(),
            EmailVerificado = true
        });

        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, idToken);

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);
    }

    // ==================================================================
    // (e) Conta criada pelo Google nasce sem senha e com papel cliente
    // ==================================================================

    [Fact]
    public async Task LoginGoogle_ComContaNova_CriaUsuarioSemSenhaEComPapelCliente()
    {
        var email = EmailNovo();

        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, _api.Google.RegistrarConta(email));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var usuario = await UsuarioDaSessaoAsync(resposta);

        Assert.False(
            usuario.GetProperty("temSenha").GetBoolean(),
            "Conta criada por Google nasceu com senha. SenhaHash tem de ficar null: nao existe " +
            "senha que a pessoa tenha escolhido.");

        Assert.True(usuario.GetProperty("ativo").GetBoolean());
        Assert.True(usuario.GetProperty("googleVinculado").GetBoolean());
        Assert.True(usuario.GetProperty("emailVerificado").GetBoolean());

        var papeis = usuario.GetProperty("roles")
            .EnumerateArray()
            .Select(p => p.GetString() ?? string.Empty)
            .ToArray();

        // Papel vem SEMPRE de usuarios_roles, e conta nova nasce cliente. Nada no payload do
        // Google pode influenciar isso — nem um papel administrativo escapa por aqui.
        Assert.Equal([Roles.Cliente], papeis);
    }

    /// <summary>
    /// Sem senha significa sem senha: o login por e-mail e senha continua fechado para essa
    /// conta, e — importante — sem revelar que ela existe.
    /// </summary>
    [Fact]
    public async Task LoginPorSenha_EmContaCriadaPeloGoogle_Retorna401ComoQualquerCredencialInvalida()
    {
        var email = EmailNovo();

        using var http = _api.CriarClienteGoogle();
        using var criacao = await Entrar(http, _api.Google.RegistrarConta(email));

        Assert.Equal(HttpStatusCode.OK, criacao.StatusCode);

        using var anonimo = _api.CriarCliente();

        using var tentativa = await anonimo.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, senha = ApiFixture.SenhaPadrao });

        await EnvelopeHttp.AssertPadraoAsync(tentativa, HttpStatusCode.Unauthorized);
    }

    // ==================================================================
    // (f) Usuario inativo nao entra
    // ==================================================================

    [Fact]
    public async Task LoginGoogle_ComUsuarioDesativado_NaoAbreSessao()
    {
        // Conta que ja existia na loja e foi desligada pelo painel.
        var cliente = await _api.RegistrarClienteAsync();
        await _api.DesativarUsuarioAsync(cliente.Id);

        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, _api.Google.RegistrarConta(cliente.Email));

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("desativada", envelope.Error, StringComparison.OrdinalIgnoreCase);

        // Nem sessao no corpo, nem cookie de refresh: desativado nao pode sair daqui com nada.
        Assert.Empty(CookiesDe(resposta));
    }

    /// <summary>
    /// O outro caminho, que e o que passa despercebido: a conta ja tem o vinculo Google gravado e
    /// so DEPOIS e desativada. Se a guarda estivesse so no casamento por e-mail, esta pessoa
    /// continuaria entrando normalmente.
    /// </summary>
    [Fact]
    public async Task LoginGoogle_ComVinculoJaExistenteEContaDesativadaDepois_NaoAbreSessao()
    {
        var sub = $"sub-desativado-{Guid.NewGuid():N}";
        var email = EmailNovo();

        using var http = _api.CriarClienteGoogle();

        using var primeira = await Entrar(http, _api.Google.RegistrarConta(email, subject: sub));
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var id = (await UsuarioDaSessaoAsync(primeira)).GetProperty("id").GetInt32();

        await _api.DesativarUsuarioAsync(id);

        using var segunda = await Entrar(http, _api.Google.RegistrarConta(email, subject: sub));

        var envelope = await EnvelopeHttp.AssertPadraoAsync(segunda, HttpStatusCode.BadRequest);

        Assert.Contains("desativada", envelope.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // (g) Token invalido -> 401 no envelope padrao, sem vazar nada
    // ==================================================================

    [Theory]
    [InlineData("id-token-que-nunca-foi-emitido")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJpbnRydXNvIn0.assinatura-forjada")]
    public async Task LoginGoogle_ComTokenInvalidoOuExpirado_Retorna401NoEnvelopePadrao(string idToken)
    {
        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, idToken);

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Unauthorized);

        var corpo = await resposta.Content.ReadAsStringAsync();

        // Nada de stack trace, nome de classe ou caminho de arquivo na resposta: a mensagem do
        // 401 e generica de proposito e o detalhe fica no log, ligado pelo traceId.
        Assert.DoesNotContain("Glorific.", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs:line", corpo, StringComparison.Ordinal);

        Assert.False(string.IsNullOrWhiteSpace(envelope.TraceId));
    }

    [Fact]
    public async Task LoginGoogle_SemIdToken_Retorna400DeValidacao()
    {
        using var http = _api.CriarClienteGoogle();

        using var resposta = await http.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "" });

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);
    }

    // ==================================================================
    // (h) Google:ClientId nao configurado -> erro claro, nunca 500
    // ==================================================================

    [Fact]
    public async Task LoginGoogle_SemClientIdConfigurado_DizQueNaoEstaConfiguradoEmVezDe500()
    {
        using var http = _api.CriarClienteSemGoogleConfigurado();

        using var resposta = await Entrar(http, "qualquer.coisa.aqui");

        Assert.NotEqual(HttpStatusCode.InternalServerError, resposta.StatusCode);

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.ServiceUnavailable);

        // A mensagem tem de dizer o que aconteceu. "Ocorreu um erro inesperado" manda o front,
        // o lojista e o suporte procurarem um bug que nao existe.
        Assert.Contains("Google", envelope.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nao esta configurado", envelope.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GoogleTokenValidator.MensagemNaoConfigurado, envelope.Error);

        // O nome da variavel de ambiente e detalhe de operacao: fica no log, nunca na resposta.
        Assert.DoesNotContain("Google__ClientId", envelope.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Google:ClientId", envelope.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Loja sem Google configurado continua vendendo: o login por e-mail e senha nao pode ser
    /// afetado pela integracao ausente.
    /// </summary>
    [Fact]
    public async Task LoginPorSenha_SemClientIdConfigurado_ContinuaFuncionando()
    {
        var cliente = await _api.RegistrarClienteAsync();

        using var http = _api.CriarClienteSemGoogleConfigurado();

        using var resposta = await http.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = cliente.Email, senha = ApiFixture.SenhaPadrao });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // ==================================================================
    // Contrato da resposta
    // ==================================================================

    [Fact]
    public async Task LoginGoogle_QuandoDaCerto_NaoDevolveRefreshTokenNoCorpoEUsaCookieHttpOnly()
    {
        using var http = _api.CriarClienteGoogle();
        using var resposta = await Entrar(http, _api.Google.RegistrarConta(EmailNovo()));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadAsStringAsync();

        // O refresh token entra e sai SO pelo cookie httpOnly. No corpo, ele viraria um valor
        // que qualquer script da pagina consegue ler.
        Assert.DoesNotContain("refreshToken", corpo, StringComparison.OrdinalIgnoreCase);

        var cookie = Assert.Single(CookiesDe(resposta), c => c.Contains("gl_rt", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static Task<HttpResponseMessage> Entrar(HttpClient cliente, string idToken) =>
        cliente.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

    private static string EmailNovo() => $"google.{Guid.NewGuid():N}@testes.glorific.art";

    private static IReadOnlyList<string> CookiesDe(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var valores) ? [.. valores] : [];

    private static async Task<JsonElement> UsuarioDaSessaoAsync(HttpResponseMessage resposta) =>
        (await ApiFixture.LerJsonAsync(resposta)).GetProperty("usuario");

    /// <summary>Le o perfil pelo painel: e a unica forma de conferir a conta de OUTRA pessoa.</summary>
    private async Task<JsonElement> PerfilAdminAsync(int idUsuario)
    {
        using var admin = await _api.CriarClienteAdminAsync();
        using var resposta = await admin.GetAsync($"/api/v1/admin/usuarios/{idUsuario}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        return await ApiFixture.LerJsonAsync(resposta);
    }
}
