using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// O fluxo do carrinho como ele acontece na loja, de ponta a ponta e sem atalho: o admin cadastra
/// a peca e gera a grade, o VISITANTE (sem conta) monta o carrinho, tenta comprar mais do que
/// existe, ajusta, remove — e, ao entrar na conta, o carrinho anonimo funde com o dele.
///
/// Tres coisas que so aparecem em teste ponta a ponta e que este arquivo cobre:
///
/// 1. IDENTIDADE DO CARRINHO. Nenhuma rota aceita id de carrinho vindo do cliente: ou e a claim
///    sub do token, ou e o cookie gl_cart. Dois visitantes diferentes nao podem se enxergar.
///
/// 2. A MENSAGEM DE ESTOQUE. "Restam apenas 5 unidade(s)" sem dizer DE QUE PECA, em que tamanho
///    e em que cor e inutil num carrinho com seis linhas. A mensagem e contrato de tela.
///
/// 3. O MERGE NO LOGIN. Somar carrinhos e onde o estoque some: somar sem olhar o disponivel
///    entrega ao cliente um carrinho que o checkout vai recusar depois.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class CarrinhoFluxoTests
{
    private const int PrecoUnitarioCentavos = 19_900;

    private readonly ApiFixture _api;

    public CarrinhoFluxoTests(ApiFixture api)
    {
        _api = api;
    }

    // ------------------------------------------------------------------
    // Visitante anonimo
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdicionarItem_VisitanteAnonimo_CriaCarrinhoEEmiteCookieDeSessao()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);
        var variacao = produto.PrimeiraVariacao;

        using var visitante = _api.CriarCliente();

        using var resposta = await AdicionarAsync(visitante, variacao.Id, quantidade: 2);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        // O cookie e a unica identidade do visitante: httpOnly (JavaScript nao le) e restrito ao
        // caminho do carrinho (um XSS em outra pagina do site nao alcanca a sessao).
        var cookie = Assert.Single(
            CookiesDeResposta(resposta),
            valor => valor.StartsWith("gl_cart=", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/carrinho", cookie, StringComparison.OrdinalIgnoreCase);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);

        Assert.Equal(2, carrinho.GetProperty("quantidadeItens").GetInt32());
        Assert.Equal(2 * PrecoUnitarioCentavos, carrinho.GetProperty("subtotalCentavos").GetInt32());

        var item = Assert.Single(carrinho.GetProperty("itens").EnumerateArray().ToArray());

        Assert.Equal(variacao.Id, item.GetProperty("idVariacao").GetInt32());
        Assert.Equal(2, item.GetProperty("quantidade").GetInt32());
        Assert.Equal(5, item.GetProperty("disponivelEmEstoque").GetInt32());
        Assert.False(item.GetProperty("indisponivel").GetBoolean());

        // A leitura seguinte, com o mesmo cookie, tem de achar o MESMO carrinho.
        using var releitura = await visitante.GetAsync("/api/v1/carrinho");
        var relido = await ApiFixture.LerJsonAsync(releitura);

        Assert.Equal(carrinho.GetProperty("uuid").GetString(), relido.GetProperty("uuid").GetString());
        Assert.Equal(2, relido.GetProperty("quantidadeItens").GetInt32());
    }

    [Fact]
    public async Task ObterCarrinho_OutroVisitante_NaoEnxergaOCarrinhoAlheio()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);

        using var primeiro = _api.CriarCliente();
        using var adicao = await AdicionarAsync(primeiro, produto.PrimeiraVariacao.Id, quantidade: 2);

        Assert.Equal(HttpStatusCode.OK, adicao.StatusCode);

        // Cliente novo = cookie novo. Sem cookie nao existe carrinho, e a API NAO cria um na
        // leitura: robo de indexacao nao pode encher a tabela de carrinhos.
        using var outro = _api.CriarCliente();
        using var resposta = await outro.GetAsync("/api/v1/carrinho");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);

        Assert.Empty(carrinho.GetProperty("itens").EnumerateArray().ToArray());
        Assert.Equal(0, carrinho.GetProperty("quantidadeItens").GetInt32());
        Assert.Empty(CookiesDeResposta(resposta));
    }

    // ------------------------------------------------------------------
    // Estoque
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdicionarItem_AcimaDoEstoque_Retorna400NomeandoPecaTamanhoECor()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);
        var variacao = produto.PrimeiraVariacao;

        using var visitante = _api.CriarCliente();

        using var primeira = await AdicionarAsync(visitante, variacao.Id, quantidade: 2);
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        // 2 que ja estao + 5 = 7, e so existem 5 na prateleira.
        using var estouro = await AdicionarAsync(visitante, variacao.Id, quantidade: 5);

        var envelope = await EnvelopeHttp.AssertPadraoAsync(estouro, HttpStatusCode.BadRequest);

        Assert.Contains("5", envelope.Error, StringComparison.Ordinal);
        Assert.Contains(produto.Nome, envelope.Error, StringComparison.Ordinal);
        Assert.Contains($"tamanho {variacao.CodigoTamanho}", envelope.Error, StringComparison.Ordinal);
        Assert.Contains($"em {variacao.NomeCor}", envelope.Error, StringComparison.Ordinal);

        // A recusa nao pode ter mexido no que ja estava no carrinho.
        using var releitura = await visitante.GetAsync("/api/v1/carrinho");
        var carrinho = await ApiFixture.LerJsonAsync(releitura);

        Assert.Equal(2, carrinho.GetProperty("quantidadeItens").GetInt32());
    }

    [Fact]
    public async Task AlterarQuantidade_AcimaDoEstoque_Retorna400ENaoAlteraALinha()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 3);
        var variacao = produto.PrimeiraVariacao;

        using var visitante = _api.CriarCliente();

        using var adicao = await AdicionarAsync(visitante, variacao.Id, quantidade: 1);
        var idItem = IdDoPrimeiroItem(await ApiFixture.LerJsonAsync(adicao));

        using var resposta = await visitante.PatchAsJsonAsync(
            $"/api/v1/carrinho/itens/{idItem}", new { quantidade = 9 });

        var envelope = await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains(produto.Nome, envelope.Error, StringComparison.Ordinal);

        using var releitura = await visitante.GetAsync("/api/v1/carrinho");
        var carrinho = await ApiFixture.LerJsonAsync(releitura);

        Assert.Equal(1, carrinho.GetProperty("quantidadeItens").GetInt32());
    }

    // ------------------------------------------------------------------
    // Alterar e remover
    // ------------------------------------------------------------------

    [Fact]
    public async Task AlterarQuantidade_DentroDoDisponivel_AtualizaLinhaESubtotal()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);

        using var visitante = _api.CriarCliente();

        using var adicao = await AdicionarAsync(visitante, produto.PrimeiraVariacao.Id, quantidade: 1);
        var idItem = IdDoPrimeiroItem(await ApiFixture.LerJsonAsync(adicao));

        using var resposta = await visitante.PatchAsJsonAsync(
            $"/api/v1/carrinho/itens/{idItem}", new { quantidade = 4 });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);

        Assert.Equal(4, carrinho.GetProperty("quantidadeItens").GetInt32());
        Assert.Equal(4 * PrecoUnitarioCentavos, carrinho.GetProperty("subtotalCentavos").GetInt32());
        Assert.Equal(4 * PrecoUnitarioCentavos, carrinho.GetProperty("totalCentavos").GetInt32());
    }

    [Fact]
    public async Task AlterarQuantidade_ParaZero_RemoveALinha()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);

        using var visitante = _api.CriarCliente();

        using var adicao = await AdicionarAsync(visitante, produto.PrimeiraVariacao.Id, quantidade: 2);
        var idItem = IdDoPrimeiroItem(await ApiFixture.LerJsonAsync(adicao));

        using var resposta = await visitante.PatchAsJsonAsync(
            $"/api/v1/carrinho/itens/{idItem}", new { quantidade = 0 });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);

        Assert.Empty(carrinho.GetProperty("itens").EnumerateArray().ToArray());
        Assert.Equal(0, carrinho.GetProperty("quantidadeItens").GetInt32());
    }

    [Fact]
    public async Task RemoverItem_DoProprioCarrinho_EsvaziaOCarrinho()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);

        using var visitante = _api.CriarCliente();

        using var adicao = await AdicionarAsync(visitante, produto.PrimeiraVariacao.Id, quantidade: 3);
        var idItem = IdDoPrimeiroItem(await ApiFixture.LerJsonAsync(adicao));

        using var resposta = await visitante.DeleteAsync($"/api/v1/carrinho/itens/{idItem}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);

        Assert.Empty(carrinho.GetProperty("itens").EnumerateArray().ToArray());
        Assert.Equal(0, carrinho.GetProperty("subtotalCentavos").GetInt32());
    }

    [Fact]
    public async Task RemoverItem_DeOutroCarrinho_Retorna400ENaoTocaNaLinhaAlheia()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 5);

        using var dono = _api.CriarCliente();
        using var adicao = await AdicionarAsync(dono, produto.PrimeiraVariacao.Id, quantidade: 2);
        var idItem = IdDoPrimeiroItem(await ApiFixture.LerJsonAsync(adicao));

        // Outro visitante, com carrinho proprio, chutando o id da linha alheia na URL.
        using var intruso = _api.CriarCliente();
        using var deleOutro = await AdicionarAsync(intruso, produto.PrimeiraVariacao.Id, quantidade: 1);
        Assert.Equal(HttpStatusCode.OK, deleOutro.StatusCode);

        using var resposta = await intruso.DeleteAsync($"/api/v1/carrinho/itens/{idItem}");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.BadRequest);

        using var releitura = await dono.GetAsync("/api/v1/carrinho");
        var carrinho = await ApiFixture.LerJsonAsync(releitura);

        Assert.Equal(2, carrinho.GetProperty("quantidadeItens").GetInt32());
    }

    // ------------------------------------------------------------------
    // Merge no login
    // ------------------------------------------------------------------

    [Fact]
    public async Task Mesclar_ComCarrinhoAnonimoEDoUsuario_SomaAsQuantidades()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 10);
        var variacao = produto.PrimeiraVariacao;

        var usuario = await _api.RegistrarClienteAsync();

        // O que o cliente ja tinha na conta, de outro dispositivo.
        using var logado = _api.CriarClienteComToken(usuario.Token);
        using var naConta = await AdicionarAsync(logado, variacao.Id, quantidade: 2);
        Assert.Equal(HttpStatusCode.OK, naConta.StatusCode);

        // O que ele montou nesta aba, ainda sem entrar na conta.
        using var visitante = _api.CriarCliente();
        using var anonimo = await AdicionarAsync(visitante, variacao.Id, quantidade: 3);
        Assert.Equal(HttpStatusCode.OK, anonimo.StatusCode);

        // Entra na conta NESTA aba: o cookie anonimo continua no navegador e o token passa a
        // acompanhar as requisicoes. E exatamente o que o front faz depois do login.
        visitante.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);

        using var resposta = await visitante.PostAsync("/api/v1/carrinho/merge", content: null);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);
        var item = Assert.Single(carrinho.GetProperty("itens").EnumerateArray().ToArray());

        Assert.Equal(variacao.Id, item.GetProperty("idVariacao").GetInt32());
        Assert.Equal(5, item.GetProperty("quantidade").GetInt32());
        Assert.Equal(5, carrinho.GetProperty("quantidadeItens").GetInt32());

        // O carrinho anonimo deixou de existir: manter o cookie faria o proximo logout devolver
        // um carrinho fantasma que nao esta mais no banco.
        Assert.Contains(
            CookiesDeResposta(resposta),
            valor => valor.StartsWith("gl_cart=", StringComparison.OrdinalIgnoreCase)
                     && valor.Contains("1970", StringComparison.Ordinal));

        // E a conta, lida de novo pelo token, ve o carrinho somado.
        using var confirmacao = await logado.GetAsync("/api/v1/carrinho");
        var doUsuario = await ApiFixture.LerJsonAsync(confirmacao);

        Assert.Equal(5, doUsuario.GetProperty("quantidadeItens").GetInt32());
    }

    [Fact]
    public async Task Mesclar_QuandoASomaPassaDoDisponivel_LimitaAoEstoqueSemPerderOQueJaHavia()
    {
        // 3 na conta + 3 no anonimo = 6, mas so existem 4 na prateleira.
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 4);
        var variacao = produto.PrimeiraVariacao;

        var usuario = await _api.RegistrarClienteAsync();

        using var logado = _api.CriarClienteComToken(usuario.Token);
        using var naConta = await AdicionarAsync(logado, variacao.Id, quantidade: 3);
        Assert.Equal(HttpStatusCode.OK, naConta.StatusCode);

        using var visitante = _api.CriarCliente();
        using var anonimo = await AdicionarAsync(visitante, variacao.Id, quantidade: 3);
        Assert.Equal(HttpStatusCode.OK, anonimo.StatusCode);

        visitante.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);

        using var resposta = await visitante.PostAsync("/api/v1/carrinho/merge", content: null);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);
        var item = Assert.Single(carrinho.GetProperty("itens").EnumerateArray().ToArray());

        // Teto no disponivel, e nunca abaixo do que o cliente ja tinha escolhido.
        Assert.Equal(4, item.GetProperty("quantidade").GetInt32());
        Assert.False(item.GetProperty("quantidadeAcimaDoDisponivel").GetBoolean());
    }

    [Fact]
    public async Task Mesclar_SemCarrinhoNaConta_AdotaOCarrinhoAnonimoInteiro()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 6);
        var variacao = produto.PrimeiraVariacao;

        var usuario = await _api.RegistrarClienteAsync();

        using var visitante = _api.CriarCliente();
        using var anonimo = await AdicionarAsync(visitante, variacao.Id, quantidade: 4);
        Assert.Equal(HttpStatusCode.OK, anonimo.StatusCode);

        visitante.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);

        using var resposta = await visitante.PostAsync("/api/v1/carrinho/merge", content: null);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var carrinho = await ApiFixture.LerJsonAsync(resposta);
        var item = Assert.Single(carrinho.GetProperty("itens").EnumerateArray().ToArray());

        Assert.Equal(4, item.GetProperty("quantidade").GetInt32());

        // Trocou de dono de verdade: um cliente HTTP novo, so com o token, ve o mesmo carrinho.
        using var outroDispositivo = _api.CriarClienteComToken(usuario.Token);
        using var confirmacao = await outroDispositivo.GetAsync("/api/v1/carrinho");

        var doUsuario = await ApiFixture.LerJsonAsync(confirmacao);

        Assert.Equal(carrinho.GetProperty("uuid").GetString(), doUsuario.GetProperty("uuid").GetString());
        Assert.Equal(4, doUsuario.GetProperty("quantidadeItens").GetInt32());
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private static Task<HttpResponseMessage> AdicionarAsync(HttpClient cliente, int idVariacao, int quantidade) =>
        cliente.PostAsJsonAsync("/api/v1/carrinho/itens", new { idVariacao, quantidade });

    private static int IdDoPrimeiroItem(JsonElement carrinho) =>
        carrinho.GetProperty("itens").EnumerateArray().First().GetProperty("id").GetInt32();

    private static IReadOnlyList<string> CookiesDeResposta(HttpResponseMessage resposta)
    {
        if (!resposta.Headers.TryGetValues("Set-Cookie", out var valores))
            return Array.Empty<string>();

        return valores.ToArray();
    }
}
