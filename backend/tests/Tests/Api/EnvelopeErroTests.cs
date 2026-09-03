using System.Net;
using System.Net.Http.Json;
using System.Text;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// O ENVELOPE UNICO de erro, conferido em TODOS os caminhos que produzem erro na API.
///
/// A afirmacao sob teste e "toda falha sai como { statusCode, error, traceId, errors? }", e ela
/// so vale se for verdadeira nos QUATRO caminhos que nasceram diferentes:
///   - middleware de excecao        -> 404 (EntityNotFoundException) e 400 (regra de negocio)
///   - InvalidModelStateResponseFactory -> 400 de validacao, com o detalhe por campo em "errors"
///   - JwtBearer OnChallenge        -> 401
///   - JwtBearer OnForbidden        -> 403
///
/// No repo de referencia esses quatro caminhos produziam quatro formatos, e o 401 do JwtBearer
/// respondia com CORPO VAZIO — o front tinha tres ramos de parse de erro e a tela de login nao
/// exibia mensagem nenhuma. Por isso cada teste aqui confere tambem que o corpo NAO e vazio.
///
/// FORA DE ESCOPO, de proposito: o 404 de rota INEXISTENTE (ex.: /api/v1/nao-existe). Ali nao ha
/// endpoint, entao nao ha middleware de aplicacao para traduzir nada — quem responde e o
/// roteamento do framework. O 404 coberto aqui e o da APLICACAO, que e o que o front consome.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class EnvelopeErroTests
{
    private readonly ApiFixture _api;

    public EnvelopeErroTests(ApiFixture api)
    {
        _api = api;
    }

    // ------------------------------------------------------------------
    // 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task RecursoPublicoInexistente_Retorna404NoEnvelope()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/api/v1/produtos/slug-que-nunca-existiu-987654321");

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.NotFound);

        Assert.False(envelope.TemDetalhePorCampo);
    }

    [Fact]
    public async Task RecursoAdministrativoInexistente_Retorna404NoEnvelope()
    {
        using var admin = await _api.CriarClienteAdminAsync();

        using var resposta = await admin.GetAsync("/api/v1/admin/produtos/987654321");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------
    // 400 de validacao (ModelState) — com o dicionario "errors"
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidacaoDeCampo_Retorna400NoEnvelopeComDicionarioErrors()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "isto-nao-e-um-email",
            senha = "curta",
            nomeCompleto = ""
        });

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);

        Assert.True(
            envelope.TemDetalhePorCampo,
            "Validacao de campo sem o dicionario 'errors' obriga o front a fazer parse da mensagem.");

        // O front destaca o campo pela CHAVE, entao ela precisa apontar para o campo de verdade.
        Assert.Contains(envelope.Errors!, par => par.Key.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(envelope.Errors!, par => par.Key.Contains("senha", StringComparison.OrdinalIgnoreCase));

        // E cada chave carrega ao menos uma mensagem legivel, nunca uma lista vazia.
        Assert.All(envelope.Errors!, par => Assert.NotEmpty(par.Value));
        Assert.All(envelope.Errors!, par => Assert.All(par.Value, m => Assert.False(string.IsNullOrWhiteSpace(m))));
    }

    /// <summary>
    /// JSON quebrado tambem e erro de ENTRADA e tem de sair no mesmo envelope — e nao como a
    /// pagina de excecao do framework nem como um 500 generico.
    /// </summary>
    [Fact]
    public async Task JsonMalFormado_Retorna400NoEnvelope()
    {
        using var anonimo = _api.CriarCliente();

        using var corpo = new StringContent("{\"email\":", Encoding.UTF8, "application/json");
        using var resposta = await anonimo.PostAsync("/api/v1/auth/login", corpo);

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // 400 de regra de negocio (BusinessValidationException)
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegraDeNegocioViolada_Retorna400NoEnvelopeComMensagemParaOUsuario()
    {
        using var anonimo = _api.CriarCliente();

        // Passa na validacao de campo (Range 1..int.MaxValue) e morre na regra: variacao que
        // nao existe. E o caminho do middleware, nao o do ModelState.
        using var resposta = await anonimo.PostAsJsonAsync("/api/v1/carrinho/itens", new
        {
            idVariacao = 987_654_321,
            quantidade = 1
        });

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);

        Assert.False(string.IsNullOrWhiteSpace(envelope.Error));

        // Mensagem de regra de negocio e para o cliente final: nao pode vazar detalhe interno.
        Assert.DoesNotContain("Exception", envelope.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", envelope.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", envelope.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // 401 e 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task SemToken_Retorna401NoEnvelope()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/api/v1/conta");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClienteEmRotaDeAdmin_Retorna403NoEnvelope()
    {
        var usuario = await _api.RegistrarClienteAsync();

        using var cliente = _api.CriarClienteComToken(usuario.Token);

        using var resposta = await cliente.GetAsync("/api/v1/admin/usuarios");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Coerencia do envelope entre os caminhos
    // ------------------------------------------------------------------

    /// <summary>
    /// Os quatro caminhos de erro respondendo com a MESMA forma, na mesma execucao. E este teste
    /// que quebra no dia em que alguem devolver { message } de um controller novo.
    /// </summary>
    [Fact]
    public async Task TodosOsCaminhosDeErro_RespondemComAMesmaForma()
    {
        var usuario = await _api.RegistrarClienteAsync();

        using var anonimo = _api.CriarCliente();
        using var cliente = _api.CriarClienteComToken(usuario.Token);

        using var naoEncontrado = await anonimo.GetAsync("/api/v1/produtos/slug-que-nunca-existiu-987654321");
        using var invalido = await anonimo.PostAsJsonAsync("/api/v1/auth/register", new { email = "x" });
        using var semToken = await anonimo.GetAsync("/api/v1/pedidos");
        using var semPermissao = await cliente.GetAsync("/api/v1/admin/configuracoes");

        await EnvelopeHttp.AssertPadraoAsync(naoEncontrado, HttpStatusCode.NotFound);
        await EnvelopeHttp.AssertPadraoAsync(invalido, HttpStatusCode.BadRequest);
        await EnvelopeHttp.AssertPadraoAsync(semToken, HttpStatusCode.Unauthorized);
        await EnvelopeHttp.AssertPadraoAsync(semPermissao, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// O traceId existe para ligar a resposta ao log do servidor. Se ele fosse constante, nao
    /// serviria para nada — duas requisicoes diferentes precisam de correlacoes diferentes.
    /// </summary>
    [Fact]
    public async Task TraceId_EDiferenteEntreDuasRequisicoes()
    {
        using var anonimo = _api.CriarCliente();

        using var primeira = await anonimo.GetAsync("/api/v1/conta");
        using var segunda = await anonimo.GetAsync("/api/v1/conta");

        var envelopePrimeira = await EnvelopeHttp.AssertPadraoAsync(primeira, HttpStatusCode.Unauthorized);
        var envelopeSegunda = await EnvelopeHttp.AssertPadraoAsync(segunda, HttpStatusCode.Unauthorized);

        Assert.NotEqual(envelopePrimeira.TraceId, envelopeSegunda.TraceId);
    }
}
