using System.Net;
using System.Text.Json;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// A vitrine ABRE para quem nunca entrou.
///
/// O outro lado da FallbackPolicy: como endpoint sem atributo passa a exigir autenticacao, uma
/// rota publica so continua publica enquanto tiver [AllowAnonymous] EXPLICITO. Remover o
/// atributo por engano nao quebra o build nem o teste de nenhum controller — quebra a loja
/// inteira para visitante e para robo de indexacao, em silencio. Estes testes sao o alarme.
///
/// Vale tambem para o /health: um health check que responde 401 faz o orquestrador matar um
/// container saudavel em laco.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class RotasPublicasTests
{
    private readonly ApiFixture _api;

    public RotasPublicasTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/catalogo")]
    [InlineData("/api/v1/catalogo/facetas")]
    [InlineData("/api/v1/categorias")]
    [InlineData("/api/v1/colecoes")]
    [InlineData("/api/v1/tamanhos")]
    [InlineData("/api/v1/cores")]
    [InlineData("/api/v1/tabelas-medidas")]
    [InlineData("/api/v1/carrinho")]
    public async Task RotaPublica_SemToken_Retorna200(string caminho)
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync(caminho);

        Assert.True(
            resposta.StatusCode == HttpStatusCode.OK,
            $"{caminho} respondeu {(int)resposta.StatusCode} para visitante anonimo. " +
            "Rota publica precisa de [AllowAnonymous] EXPLICITO por causa da FallbackPolicy.");

        // Desafio de autenticacao em rota publica seria o sintoma do mesmo erro.
        Assert.Empty(resposta.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task Health_SemToken_RespondeSaudavel()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", (await resposta.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Catalogo_SemToken_RetornaPaginaComOFormatoDePagedResult()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/api/v1/catalogo");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        // Toda listagem da API e paginada — nunca uma colecao crua.
        Assert.Equal(JsonValueKind.Array, corpo.GetProperty("items").ValueKind);
        Assert.True(corpo.GetProperty("page").GetInt32() >= 1);
        Assert.True(corpo.GetProperty("pageSize").GetInt32() >= 1);
        Assert.True(corpo.GetProperty("total").GetInt32() >= 0);
    }

    [Fact]
    public async Task Catalogo_ComProdutoPublicadoEEmEstoque_TrazOProdutoNaBusca()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 3);

        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync(
            $"/api/v1/catalogo?q={Uri.EscapeDataString(produto.Nome)}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        var slugs = corpo
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString())
            .ToArray();

        Assert.Contains(produto.Slug, slugs);
    }

    [Fact]
    public async Task PaginaDeProduto_SemToken_RetornaODetalhePeloSlug()
    {
        var produto = await _api.CriarProdutoComGradeAsync(estoqueInicial: 2);

        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync($"/api/v1/produtos/{produto.Slug}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        Assert.Equal(produto.Slug, corpo.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Tamanhos_SemToken_TrazAGradeSemeadaNaOrdemDeExibicao()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/api/v1/tamanhos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var itens = (await ApiFixture.LerJsonAsync(resposta)).EnumerateArray().ToArray();

        Assert.NotEmpty(itens);

        // Ordem de EXIBICAO (grade, depois ordem), nunca alfabetica: "GG" viria antes de "P".
        var chaves = itens
            .Select(item => (Grade: item.GetProperty("grade").GetInt32(), Ordem: item.GetProperty("ordem").GetInt32()))
            .ToArray();

        Assert.Equal(chaves.OrderBy(c => c.Grade).ThenBy(c => c.Ordem).ToArray(), chaves);
    }

    [Fact]
    public async Task Cores_SemToken_TrazAsCoresSemeadasComHexValido()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/api/v1/cores");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var itens = (await ApiFixture.LerJsonAsync(resposta)).EnumerateArray().ToArray();

        Assert.NotEmpty(itens);

        // O front pinta a bolinha direto com este valor: hex fora do formato some da tela.
        Assert.All(itens, item =>
        {
            var hex = item.GetProperty("hexRgb").GetString();

            Assert.NotNull(hex);
            Assert.Equal(7, hex!.Length);
            Assert.StartsWith("#", hex, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// O Swagger publica o mapa completo da API, incluindo as rotas administrativas, e por isso
    /// fica FORA de producao. No ambiente de teste ele tem de estar de pe — e o contrato que o
    /// front consome para gerar cliente.
    /// </summary>
    [Fact]
    public async Task Swagger_ForaDeProducao_PublicaOContratoDaApi()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        Assert.True(corpo.TryGetProperty("paths", out var caminhos));
        Assert.True(caminhos.TryGetProperty("/api/v1/catalogo", out _));
    }
}
