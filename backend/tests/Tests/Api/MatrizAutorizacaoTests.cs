using System.Net;
using System.Text;
using Glorific.Domain.Constants;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// A MATRIZ DE AUTORIZACAO: para cada grupo de rota, quem entra, quem leva 401 e quem leva 403.
///
/// E a suite que a especificacao do repo de referencia exigia e que nunca foi escrita. O preco
/// disso ja e conhecido: seis controllers ficaram publicos por OMISSAO de atributo, dois deles
/// com "// TODO: Apenas para ADMINS" no cabecalho, e ninguem descobriu porque nenhum teste
/// perguntava "e se eu chamar isso sem token?".
///
/// Tres perguntas por rota, sempre as mesmas:
///   1. anonimo em rota protegida  -> 401 no envelope { statusCode, error, traceId }
///   2. cliente em rota de admin   -> 403 no MESMO envelope
///   3. admin em rota de admin     -> nem 401 nem 403 (o status util nao interessa aqui)
///
/// Mais a fronteira ENTRE papeis administrativos, que e onde o estrago e silencioso: gerente nao
/// entra em /admin/usuarios (conceder papel e a operacao mais perigosa do sistema) e operador de
/// expedicao nao mexe em catalogo nem em preco.
///
/// As duas primeiras perguntas passam pelo caminho do JwtBearer (OnChallenge/OnForbidden), que
/// por PADRAO do framework responde com CORPO VAZIO — por isso a conferencia do envelope aqui
/// nao e detalhe cosmetico: e o que impede o 401 de voltar a ser uma tela sem mensagem.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class MatrizAutorizacaoTests
{
    private readonly ApiFixture _api;

    public MatrizAutorizacaoTests(ApiFixture api)
    {
        _api = api;
    }

    // ==================================================================
    // 1. Anonimo em rota protegida -> 401 com envelope
    // ==================================================================

    [Theory]
    // Painel: catalogo e preco (GestaoCatalogo)
    [InlineData("GET", "/api/v1/admin/produtos")]
    [InlineData("GET", "/api/v1/admin/produtos/inativos")]
    [InlineData("GET", "/api/v1/admin/categorias")]
    [InlineData("GET", "/api/v1/admin/colecoes")]
    [InlineData("GET", "/api/v1/admin/cores")]
    [InlineData("GET", "/api/v1/admin/tamanhos")]
    [InlineData("GET", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/avaliacoes")]
    [InlineData("GET", "/api/v1/admin/tabelas-medidas")]
    [InlineData("GET", "/api/v1/admin/midias")]
    [InlineData("POST", "/api/v1/admin/produtos")]
    [InlineData("POST", "/api/v1/admin/cupons")]
    // Painel: expedicao
    [InlineData("GET", "/api/v1/admin/pedidos")]
    [InlineData("GET", "/api/v1/admin/estoque/alerta-minimo")]
    // Painel: somente admin
    [InlineData("GET", "/api/v1/admin/usuarios")]
    [InlineData("GET", "/api/v1/admin/configuracoes")]
    [InlineData("PUT", "/api/v1/admin/configuracoes")]
    // Painel: porta de entrada
    [InlineData("GET", "/api/v1/admin/dashboard")]
    // Area do cliente
    [InlineData("GET", "/api/v1/conta")]
    [InlineData("PUT", "/api/v1/conta")]
    [InlineData("GET", "/api/v1/conta/enderecos")]
    [InlineData("POST", "/api/v1/conta/enderecos")]
    [InlineData("GET", "/api/v1/pedidos")]
    [InlineData("POST", "/api/v1/checkout")]
    [InlineData("GET", "/api/v1/lista-desejos")]
    [InlineData("GET", "/api/v1/lista-desejos/ids")]
    [InlineData("POST", "/api/v1/lista-desejos")]
    [InlineData("POST", "/api/v1/carrinho/merge")]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("POST", "/api/v1/auth/logout-all")]
    [InlineData("POST", "/api/v1/auth/change-password")]
    // Sem atributo na classe: quem protege e a FallbackPolicy do Program.
    [InlineData("POST", "/api/v1/avaliacoes")]
    public async Task Rota_AnonimoEmRotaProtegida_Retorna401ComEnvelope(string metodo, string caminho)
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.SendAsync(Requisicao(metodo, caminho));

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Unauthorized);

        // Mensagem generica de proposito: dizer POR QUE a credencial falhou entrega informacao
        // util a quem esta adivinhando.
        Assert.DoesNotContain("senha", envelope.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("token-que-nao-e-jwt")]
    [InlineData("aaa.bbb.ccc")]
    // Formato de JWT valido, assinatura que nao e nossa: e a tentativa de forjar um token.
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJpbnRydXNvIiwicm9sZSI6ImFkbWluIn0.assinatura-forjada")]
    public async Task Rota_TokenInvalidoEmRotaProtegida_Retorna401ComEnvelope(string token)
    {
        using var cliente = _api.CriarClienteComToken(token);

        using var resposta = await cliente.GetAsync("/api/v1/conta");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Unauthorized);
    }

    // ==================================================================
    // 2. Cliente autenticado em rota de admin -> 403 com envelope
    // ==================================================================

    [Theory]
    [InlineData("GET", "/api/v1/admin/produtos")]
    [InlineData("GET", "/api/v1/admin/produtos/inativos")]
    [InlineData("GET", "/api/v1/admin/categorias")]
    [InlineData("GET", "/api/v1/admin/colecoes")]
    [InlineData("GET", "/api/v1/admin/cores")]
    [InlineData("GET", "/api/v1/admin/tamanhos")]
    [InlineData("GET", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/avaliacoes")]
    [InlineData("GET", "/api/v1/admin/tabelas-medidas")]
    [InlineData("GET", "/api/v1/admin/midias")]
    [InlineData("POST", "/api/v1/admin/produtos")]
    [InlineData("POST", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/pedidos")]
    [InlineData("GET", "/api/v1/admin/estoque/alerta-minimo")]
    [InlineData("GET", "/api/v1/admin/usuarios")]
    [InlineData("GET", "/api/v1/admin/configuracoes")]
    [InlineData("PUT", "/api/v1/admin/configuracoes")]
    [InlineData("GET", "/api/v1/admin/dashboard")]
    public async Task Rota_ClienteEmRotaDeAdmin_Retorna403ComEnvelope(string metodo, string caminho)
    {
        var usuario = await _api.RegistrarClienteAsync();

        using var cliente = _api.CriarClienteComToken(usuario.Token);

        using var resposta = await cliente.SendAsync(Requisicao(metodo, caminho));

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Forbidden);
    }

    // ==================================================================
    // 3. Admin em rota de admin -> nem 401 nem 403
    // ==================================================================

    [Theory]
    [InlineData("GET", "/api/v1/admin/produtos")]
    [InlineData("GET", "/api/v1/admin/produtos/inativos")]
    [InlineData("GET", "/api/v1/admin/categorias")]
    [InlineData("GET", "/api/v1/admin/colecoes")]
    [InlineData("GET", "/api/v1/admin/cores")]
    [InlineData("GET", "/api/v1/admin/tamanhos")]
    [InlineData("GET", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/avaliacoes")]
    [InlineData("GET", "/api/v1/admin/tabelas-medidas")]
    [InlineData("GET", "/api/v1/admin/midias")]
    [InlineData("GET", "/api/v1/admin/pedidos")]
    [InlineData("GET", "/api/v1/admin/estoque/alerta-minimo")]
    [InlineData("GET", "/api/v1/admin/usuarios")]
    [InlineData("GET", "/api/v1/admin/configuracoes")]
    [InlineData("GET", "/api/v1/admin/dashboard")]
    public async Task Rota_AdminEmRotaDeAdmin_NaoRetorna401Nem403(string metodo, string caminho)
    {
        using var admin = await _api.CriarClienteAdminAsync();

        using var resposta = await admin.SendAsync(Requisicao(metodo, caminho));

        AssertAutorizado(resposta, caminho);
    }

    [Fact]
    public async Task ListagemDoPainel_ComTokenDeAdmin_Retorna200()
    {
        using var admin = await _api.CriarClienteAdminAsync();

        using var resposta = await admin.GetAsync("/api/v1/admin/produtos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // ==================================================================
    // 4. Cliente em rota de cliente -> nem 401 nem 403
    // ==================================================================

    [Theory]
    [InlineData("GET", "/api/v1/conta")]
    [InlineData("GET", "/api/v1/conta/enderecos")]
    [InlineData("GET", "/api/v1/pedidos")]
    [InlineData("GET", "/api/v1/lista-desejos")]
    [InlineData("GET", "/api/v1/lista-desejos/ids")]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("POST", "/api/v1/carrinho/merge")]
    [InlineData("POST", "/api/v1/checkout")]
    public async Task Rota_ClienteEmRotaDeCliente_NaoRetorna401Nem403(string metodo, string caminho)
    {
        var usuario = await _api.RegistrarClienteAsync();

        using var cliente = _api.CriarClienteComToken(usuario.Token);

        using var resposta = await cliente.SendAsync(Requisicao(metodo, caminho));

        AssertAutorizado(resposta, caminho);
    }

    [Fact]
    public async Task Conta_ComTokenDeCliente_Retorna200ComOProprioPerfil()
    {
        var usuario = await _api.RegistrarClienteAsync();

        using var cliente = _api.CriarClienteComToken(usuario.Token);

        using var resposta = await cliente.GetAsync("/api/v1/conta");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        // O perfil sai do token, nunca de um id vindo do cliente.
        Assert.Equal(usuario.Uuid, corpo.GetProperty("uuid").GetString());
        Assert.Equal(usuario.Email, corpo.GetProperty("email").GetString());
    }

    // ==================================================================
    // 5. Fronteira ENTRE papeis administrativos
    // ==================================================================

    /// <summary>
    /// Gerente cuida de catalogo e preco, mas NAO administra usuarios nem a configuracao da
    /// loja. Quem concede o papel "admin" consegue tudo o mais — por isso essa porta e a unica
    /// com policy propria.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/v1/admin/usuarios")]
    [InlineData("GET", "/api/v1/admin/configuracoes")]
    [InlineData("PUT", "/api/v1/admin/configuracoes")]
    public async Task Rota_GerenteEmRotaSomenteAdmin_Retorna403ComEnvelope(string metodo, string caminho)
    {
        using var gerente = _api.CriarClienteComToken(await _api.TokenPapelAsync(Roles.Gerente));

        using var resposta = await gerente.SendAsync(Requisicao(metodo, caminho));

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/produtos")]
    [InlineData("GET", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/pedidos")]
    [InlineData("GET", "/api/v1/admin/dashboard")]
    public async Task Rota_GerenteEmRotaDeCatalogoOuExpedicao_NaoRetorna401Nem403(string metodo, string caminho)
    {
        using var gerente = _api.CriarClienteComToken(await _api.TokenPapelAsync(Roles.Gerente));

        using var resposta = await gerente.SendAsync(Requisicao(metodo, caminho));

        AssertAutorizado(resposta, caminho);
    }

    /// <summary>
    /// Operador de expedicao enxerga pedido e envio. Catalogo, preco, cupom e usuario ficam
    /// fora — e o teste que impede alguem de "simplificar" trocando a policy do controller de
    /// catalogo por PainelAdmin.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/v1/admin/produtos")]
    [InlineData("GET", "/api/v1/admin/cupons")]
    [InlineData("GET", "/api/v1/admin/categorias")]
    [InlineData("GET", "/api/v1/admin/midias")]
    [InlineData("GET", "/api/v1/admin/usuarios")]
    [InlineData("GET", "/api/v1/admin/configuracoes")]
    public async Task Rota_OperadorEmRotaDeCatalogoOuAdmin_Retorna403ComEnvelope(string metodo, string caminho)
    {
        using var operador = _api.CriarClienteComToken(await _api.TokenPapelAsync(Roles.Operador));

        using var resposta = await operador.SendAsync(Requisicao(metodo, caminho));

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/pedidos")]
    [InlineData("GET", "/api/v1/admin/estoque/alerta-minimo")]
    [InlineData("GET", "/api/v1/admin/dashboard")]
    public async Task Rota_OperadorEmRotaDeExpedicao_NaoRetorna401Nem403(string metodo, string caminho)
    {
        using var operador = _api.CriarClienteComToken(await _api.TokenPapelAsync(Roles.Operador));

        using var resposta = await operador.SendAsync(Requisicao(metodo, caminho));

        AssertAutorizado(resposta, caminho);
    }

    // ==================================================================
    // Apoio
    // ==================================================================

    private static HttpRequestMessage Requisicao(string metodo, string caminho)
    {
        var requisicao = new HttpRequestMessage(new HttpMethod(metodo), caminho);

        // Corpo vazio e suficiente: a autorizacao roda ANTES do model binding, entao um 400 de
        // validacao aqui ja significa "passou pela porta" — que e exatamente o que se afirma.
        if (!string.Equals(metodo, "GET", StringComparison.Ordinal))
            requisicao.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return requisicao;
    }

    private static void AssertAutorizado(HttpResponseMessage resposta, string caminho)
    {
        var status = (int)resposta.StatusCode;

        Assert.True(
            status != 401 && status != 403,
            $"{caminho}: quem tem o papel certo levou {status}. " +
            "Autorizacao apertada demais tranca o painel por fora.");
    }
}
