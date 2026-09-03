using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Api;

/// <summary>
/// O guia de medidas PUBLICO — GET /api/v1/tabelas-medidas.
///
/// Ate agora so existia o CRUD do painel. A pagina /guia-de-medidas da loja e lida por quem
/// ainda nao tem conta: e justamente ANTES de comprar que a pessoa precisa saber se veste M ou
/// G. Guia atras de login e guia que ninguem le — e devolucao por tamanho errado e o custo
/// numero 1 de uma loja de moda.
///
/// Tres coisas sao verificadas aqui e nenhuma delas e cosmetica:
///   1. a rota abre para visitante anonimo (a FallbackPolicy do projeto exige [AllowAnonymous]
///      EXPLICITO; sem ele a rota nasce fechada e ninguem percebe ate a loja estar no ar);
///   2. tabela DESATIVADA nao vaza, nem na lista nem pelo id direto;
///   3. o contrato publico e ESTREITO — sem a flag Ativo, sem o id interno da linha — e
///      continua estreito quando o painel ganhar campos novos.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class TabelasMedidasPublicasTests
{
    private const string Rota = "/api/v1/tabelas-medidas";

    private readonly ApiFixture _api;

    public TabelasMedidasPublicasTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Listar_SemToken_Retorna200SemDesafioDeAutenticacao()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync(Rota);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        // Desafio de autenticacao em rota publica e o sintoma de [AllowAnonymous] esquecido.
        Assert.Empty(resposta.Headers.WwwAuthenticate);

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        // Colecao crua, nao PagedResult: a pagina exibe o guia inteiro de uma vez, e paginar
        // obrigaria o front a varrer paginas para montar uma tela unica.
        Assert.Equal(JsonValueKind.Array, corpo.ValueKind);
    }

    [Fact]
    public async Task Listar_ComTabelaAtiva_DevolveOContratoCombinadoComAsLinhasOrdenadas()
    {
        var criada = await CriarTabelaAsync(ativa: true);

        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync(Rota);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var tabela = LocalizarPorId(await ApiFixture.LerJsonAsync(resposta), criada.Id);

        Assert.NotNull(tabela);
        ConferirContrato(tabela!.Value, criada);
    }

    [Fact]
    public async Task Obter_ComTabelaAtiva_DevolveOMesmoObjetoDaListagem()
    {
        var criada = await CriarTabelaAsync(ativa: true);

        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync($"{Rota}/{criada.Id}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        ConferirContrato(await ApiFixture.LerJsonAsync(resposta), criada);
    }

    // ==================================================================
    // Tabela desativada nao vaza
    // ==================================================================

    [Fact]
    public async Task Listar_ComTabelaInativa_NaoADevolve()
    {
        var inativa = await CriarTabelaAsync(ativa: false);

        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync(Rota);

        var encontrada = LocalizarPorId(await ApiFixture.LerJsonAsync(resposta), inativa.Id);

        Assert.True(
            encontrada is null,
            "Tabela de medidas DESATIVADA apareceu na listagem publica. O admin desativa " +
            "justamente para tirar do ar uma grade errada — e ela continuaria orientando compra.");
    }

    [Fact]
    public async Task Obter_ComTabelaInativa_Retorna404IgualAInexistente()
    {
        var inativa = await CriarTabelaAsync(ativa: false);

        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync($"{Rota}/{inativa.Id}");

        // 404, e nao 403: distinguir "existe mas voce nao pode ver" de "nao existe" conta ao
        // visitante o que ha no painel.
        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Obter_ComIdInexistente_Retorna404NoEnvelopePadrao()
    {
        using var anonimo = _api.CriarCliente();

        using var resposta = await anonimo.GetAsync($"{Rota}/987654321");

        await EnvelopeHttp.AssertPadraoAsync(resposta, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Obter_ComIdNaoNumerico_NaoCasaComNenhumaAction()
    {
        // Cliente AUTENTICADO de proposito. Requisicao que nao casa com endpoint nenhum tambem
        // passa pela FallbackPolicy do projeto, entao para o anonimo o 404 sairia disfarcado de
        // 401 e o teste nao provaria nada sobre roteamento.
        using var admin = await _api.CriarClienteAdminAsync();

        // A restricao {id:int} existe para isso: sem ela, /tabelas-medidas/abc casaria com a
        // action de detalhe e o binding transformaria a URL do visitante em erro de servidor.
        using var resposta = await admin.GetAsync($"{Rota}/nao-e-numero");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    // ==================================================================
    // Contrato estreito
    // ==================================================================

    [Fact]
    public async Task Obter_NaoExpoeCamposAdministrativos()
    {
        var criada = await CriarTabelaAsync(ativa: true);

        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync($"{Rota}/{criada.Id}");

        var tabela = await ApiFixture.LerJsonAsync(resposta);

        Assert.False(
            tabela.TryGetProperty("ativo", out _),
            "A resposta publica devolveu a flag 'ativo'. O publico so enxerga tabela ativa — o " +
            "campo so daria ao front uma decisao que ele nao precisa (e pode) tomar.");

        foreach (var linha in tabela.GetProperty("linhas").EnumerateArray())
        {
            Assert.False(
                linha.TryGetProperty("id", out _),
                "A linha publica devolveu o 'id' interno, que so serve para a edicao no painel.");
        }
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// O que o teste espera encontrar na resposta publica, montado a partir do que ele acabou de
    /// cadastrar pelo painel.
    /// </summary>
    private sealed record TabelaCriada(
        int Id,
        string Nome,
        string Observacao,
        IReadOnlyList<LinhaCriada> LinhasNaOrdemEsperada);

    private sealed record LinhaCriada(int IdTamanho, string CodigoTamanho, int Ordem, decimal Busto);

    /// <summary>
    /// Cria uma tabela pelo painel com as linhas FORA de ordem de proposito: e a unica forma de
    /// provar que a ordenacao vem do servidor, e nao da ordem em que as linhas foram gravadas.
    /// </summary>
    private async Task<TabelaCriada> CriarTabelaAsync(bool ativa)
    {
        using var admin = await _api.CriarClienteAdminAsync();

        var tamanhos = await TamanhosAsync();
        var sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        // Ordens 3, 1, 2 na sequencia enviada.
        var enviadas = new[]
        {
            new { tamanho = tamanhos[2], ordem = 3, busto = 96m },
            new { tamanho = tamanhos[0], ordem = 1, busto = 84m },
            new { tamanho = tamanhos[1], ordem = 2, busto = 90m }
        };

        var nome = $"Guia de medidas {sufixo}";
        var observacao = "Meça sobre a roupa de baixo, sem apertar a fita.";

        using var resposta = await admin.PostAsJsonAsync(Rota.Replace("/v1/", "/v1/admin/", StringComparison.Ordinal), new
        {
            nome,
            observacao,
            ativo = ativa,
            linhas = enviadas.Select(l => new
            {
                idTamanho = l.tamanho.Id,
                bustoCm = l.busto,
                cinturaCm = l.busto - 20,
                quadrilCm = l.busto + 4,
                comprimentoCm = 70m,
                mangaCm = 58m,
                ordem = l.ordem
            }).ToArray()
        });

        Assert.True(
            resposta.IsSuccessStatusCode,
            $"Nao foi possivel criar a tabela de medidas de teste: {(int)resposta.StatusCode} " +
            $"{await resposta.Content.ReadAsStringAsync()}");

        var corpo = await ApiFixture.LerJsonAsync(resposta);

        return new TabelaCriada(
            corpo.GetProperty("id").GetInt32(),
            nome,
            observacao,
            [.. enviadas
                .OrderBy(l => l.ordem)
                .Select(l => new LinhaCriada(l.tamanho.Id, l.tamanho.Codigo, l.ordem, l.busto))]);
    }

    private static void ConferirContrato(JsonElement tabela, TabelaCriada esperada)
    {
        Assert.Equal(esperada.Id, tabela.GetProperty("id").GetInt32());
        Assert.Equal(esperada.Nome, tabela.GetProperty("nome").GetString());
        Assert.Equal(esperada.Observacao, tabela.GetProperty("observacao").GetString());

        var linhas = tabela.GetProperty("linhas").EnumerateArray().ToArray();

        Assert.Equal(esperada.LinhasNaOrdemEsperada.Count, linhas.Length);

        for (var indice = 0; indice < linhas.Length; indice++)
        {
            var linha = linhas[indice];
            var referencia = esperada.LinhasNaOrdemEsperada[indice];

            Assert.Equal(referencia.IdTamanho, linha.GetProperty("idTamanho").GetInt32());

            // Sem o codigo do tamanho a primeira coluna do guia sai vazia — e o guia inteiro
            // perde a serventia, porque ninguem sabe a que tamanho a linha se refere.
            Assert.Equal(referencia.CodigoTamanho, linha.GetProperty("codigoTamanho").GetString());

            Assert.Equal(referencia.Ordem, linha.GetProperty("ordemTamanho").GetInt32());
            Assert.Equal(referencia.Busto, linha.GetProperty("bustoCm").GetDecimal());

            foreach (var medida in new[] { "cinturaCm", "quadrilCm", "comprimentoCm", "mangaCm" })
                Assert.True(linha.TryGetProperty(medida, out _), $"A linha publica nao devolveu '{medida}'.");
        }

        // Ordenacao crescente, feita pelo servidor: as linhas foram gravadas fora de ordem.
        var ordens = linhas.Select(l => l.GetProperty("ordemTamanho").GetInt32()).ToArray();

        Assert.Equal([.. ordens.OrderBy(o => o)], ordens);
    }

    private static JsonElement? LocalizarPorId(JsonElement lista, int id)
    {
        foreach (var item in lista.EnumerateArray())
        {
            if (item.GetProperty("id").GetInt32() == id)
                return item;
        }

        return null;
    }

    private sealed record TamanhoDeTeste(int Id, string Codigo);

    private async Task<IReadOnlyList<TamanhoDeTeste>> TamanhosAsync()
    {
        using var anonimo = _api.CriarCliente();
        using var resposta = await anonimo.GetAsync("/api/v1/tamanhos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var tamanhos = (await ApiFixture.LerJsonAsync(resposta))
            .EnumerateArray()
            .Select(t => new TamanhoDeTeste(t.GetProperty("id").GetInt32(), t.GetProperty("codigo").GetString() ?? string.Empty))
            .ToArray();

        Assert.True(tamanhos.Length >= 3, "O seed precisa de ao menos tres tamanhos para montar a grade do teste.");

        return tamanhos;
    }
}
