using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Glorific.Tests.TestSupport;

/// <summary>
/// Sobe a API DE VERDADE (WebApplicationFactory) contra um Postgres DE VERDADE
/// (Testcontainers), uma vez por sessao de teste.
///
/// POR QUE POSTGRES REAL, E NUNCA SQLITE: o schema deste projeto depende de coisas que o SQLite
/// nao tem — jsonb, xmin como token de concorrencia, "timestamp without time zone", indice unico
/// PARCIAL (carrinho aberto por sessao) e ON DELETE. Um teste verde no SQLite prova apenas que o
/// SQLite aceitou; foi exatamente assim que o repo de referencia levou para producao um schema
/// que o Postgres recusava.
///
/// POR QUE VARIAVEL DE AMBIENTE, E NAO ConfigureAppConfiguration: com o hosting minimo, o
/// Program.cs LE a configuracao antes de <c>builder.Build()</c> (RequiredSecret, AddInfrastructure,
/// AddAutenticacao). Tudo o que o WebApplicationFactory injeta por ConfigureAppConfiguration/
/// UseSetting so entra NO Build, isto e, tarde demais para essas leituras. Variavel de ambiente e
/// lida no <c>WebApplication.CreateBuilder</c>, que e o unico ponto anterior a todas elas.
/// Por isso a porta dinamica do container so pode chegar na API por aqui — e por isso o host e
/// construido AINDA DENTRO do InitializeAsync, logo apos publicar as variaveis, fechando a janela
/// em que outra fixture da mesma sessao poderia sobrescrever a connection string.
///
/// O boot aplica migration e seed (papeis, tamanhos, cores, configuracao da loja) e cria o admin
/// a partir de ADMIN_EMAIL/ADMIN_SENHA — as mesmas variaveis do deploy, sem atalho de teste.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>Nao e Development: o objetivo e exercitar o caminho "ambiente nao-dev", com todo
    /// segredo exigido pelo fail-fast do boot presente e nenhuma leniencia de desenvolvimento.</summary>
    public const string Ambiente = "Testing";

    /// <summary>48 caracteres: o boot exige no minimo 32 para HS256.</summary>
    public const string ChaveJwt = "chave-de-teste-glorific-hs256-com-48-caracteres!";

    public const string EmailAdmin = "admin.testes@glorific.art";

    /// <summary>SeedAdmin exige 12+ caracteres.</summary>
    public const string SenhaAdmin = "Senha-Admin-De-Teste-2026";

    /// <summary>Senha usada por todo usuario criado pelos testes.</summary>
    public const string SenhaPadrao = "Senha-De-Teste-2026";

    // Mesma imagem do docker-compose: testar contra uma versao diferente da de producao e
    // como nao testar o schema. A imagem vai no construtor porque o construtor sem parametros
    // do PostgreSqlBuilder esta obsoleto (Testcontainers 4.14+).
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("glorific_testes")
        .WithUsername("glorific")
        .WithPassword("glorific")
        .Build();

    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly Dictionary<string, string> _tokensPorPapel = new(StringComparer.Ordinal);

    private WebApplicationFactory<global::Program>? _fabrica;
    private WebApplicationFactory<global::Program>? _fabricaGoogle;
    private WebApplicationFactory<global::Program>? _fabricaSemGoogle;
    private string? _tokenAdmin;
    private int? _idCategoria;
    private IReadOnlyList<int>? _idsTamanhos;
    private IReadOnlyList<int>? _idsCores;

    public WebApplicationFactory<global::Program> Fabrica =>
        _fabrica ?? throw new InvalidOperationException("A fixture ainda nao foi inicializada.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        PublicarConfiguracaoNoAmbiente(_postgres.GetConnectionString());

        _fabrica = new WebApplicationFactory<global::Program>();

        // Forca o boot AGORA, dentro da janela em que as variaveis acabaram de ser publicadas.
        // De quebra, uma falha de migration/seed aparece aqui e nao espalhada por 40 testes.
        using var cliente = _fabrica.CreateClient();
        using var resposta = await cliente.GetAsync("/health");

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"A API subiu mas /health respondeu {(int)resposta.StatusCode}. " +
                "Verifique o container do Postgres e as variaveis publicadas pela ApiFixture.");
        }

        // /health so prova que o Postgres responde — inclusive um banco VAZIO. Esta leitura
        // prova que migration e seed rodaram no boot, e falha aqui, uma vez, em vez de virar
        // "relation nao existe" espalhado por dezenas de testes.
        await IdsTamanhosAsync();
    }

    public async Task DisposeAsync()
    {
        _fabricaSemGoogle?.Dispose();
        _fabricaGoogle?.Dispose();
        _fabrica?.Dispose();
        _trava.Dispose();

        await _postgres.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // Clientes HTTP
    // ------------------------------------------------------------------

    /// <summary>Cliente sem token. Guarda cookies — e o que o carrinho anonimo precisa.</summary>
    public HttpClient CriarCliente() => Fabrica.CreateClient();

    public HttpClient CriarClienteComToken(string token)
    {
        var cliente = CriarCliente();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    public async Task<HttpClient> CriarClienteAdminAsync() =>
        CriarClienteComToken(await TokenAdminAsync());

    // ------------------------------------------------------------------
    // Login com Google
    // ------------------------------------------------------------------

    /// <summary>
    /// O dublê da porta de validacao do id_token, compartilhado pela sessao.
    ///
    /// Existe porque um id_token que passe pela validacao de verdade so pode ser produzido com a
    /// chave privada do Google. Sem ele, TUDO o que vem depois da validacao — casamento por
    /// e-mail verificado, conta desativada, criacao de conta sem senha, papel cliente — nunca e
    /// executado, que era exatamente o estado deste fluxo ate agora.
    /// </summary>
    public GoogleDeTeste Google { get; } = new();

    /// <summary>
    /// Cliente anonimo contra uma API cujo IGoogleTokenValidator e o <see cref="Google"/>.
    ///
    /// UM host para a sessao inteira, e nao um por teste: subir host novo reaplica migration e
    /// seed a cada vez. O dublê distingue os cenarios pelo proprio id_token, entao um host so
    /// atende todos eles.
    /// </summary>
    public HttpClient CriarClienteGoogle()
    {
        _fabricaGoogle ??= FabricaCom(servicos =>
        {
            // RemoveAll antes do Add: o registro real usa TryAddSingleton e ja aconteceu quando
            // o ConfigureTestServices roda. Sem remover, o dublê seria ignorado em silencio e o
            // teste passaria a exercitar o validador de verdade — que recusa tudo.
            servicos.RemoveAll<IGoogleTokenValidator>();
            servicos.AddSingleton<IGoogleTokenValidator>(Google);
        });

        return _fabricaGoogle.CreateClient();
    }

    /// <summary>
    /// Cliente contra uma API em que Google:ClientId ficou com o placeholder do appsettings —
    /// a loja que nunca configurou o login com Google.
    ///
    /// O validador aqui e o DE VERDADE, de proposito: o que esta sob teste e o caminho de
    /// configuracao ausente do proprio adaptador.
    /// </summary>
    public HttpClient CriarClienteSemGoogleConfigurado()
    {
        _fabricaSemGoogle ??= FabricaCom(servicos =>
            servicos.PostConfigure<GoogleOptions>(opcoes => opcoes.ClientId = SegredoPlaceholder.Valor));

        return _fabricaSemGoogle.CreateClient();
    }

    /// <summary>
    /// Uma API igual a de sempre, com os servicos que o teste mandar TROCADOS.
    ///
    /// O host novo compartilha o MESMO banco (as variaveis de ambiente ja estao publicadas), o
    /// que e proposital: o teste precisa enxergar o usuario que ele acabou de cadastrar pela API
    /// normal.
    /// </summary>
    public WebApplicationFactory<global::Program> FabricaCom(Action<IServiceCollection> configurar) =>
        Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(configurar));

    /// <summary>Desativa um usuario pelo painel — o mesmo caminho que o admin usa.</summary>
    public async Task DesativarUsuarioAsync(int idUsuario)
    {
        using var admin = await CriarClienteAdminAsync();

        using var resposta = await admin.PostAsync($"/api/v1/admin/usuarios/{idUsuario}/desativar", content: null);

        await GarantirSucessoAsync(resposta, $"desativar o usuario {idUsuario}");
    }

    // ------------------------------------------------------------------
    // Tokens
    // ------------------------------------------------------------------

    /// <summary>Token do admin criado pelo SeedAdmin no boot. Emitido uma vez e reaproveitado.</summary>
    public async Task<string> TokenAdminAsync()
    {
        if (_tokenAdmin is not null)
            return _tokenAdmin;

        await _trava.WaitAsync();

        try
        {
            _tokenAdmin ??= await LogarAsync(EmailAdmin, SenhaAdmin);
            return _tokenAdmin;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>
    /// Token de um papel administrativo que NAO e admin (gerente, operador).
    ///
    /// O papel e concedido pelo proprio painel (/admin/usuarios/{id}/roles/{papel}) e o token e
    /// emitido DEPOIS: papel novo so vale no proximo login, porque quem carrega a autorizacao e
    /// a claim role do JWT ja assinado.
    /// </summary>
    public async Task<string> TokenPapelAsync(string papel)
    {
        // FORA da trava de proposito: TokenAdminAsync usa a MESMA trava e SemaphoreSlim nao e
        // reentrante — pedir o token do admin la dentro travaria a suite inteira.
        var tokenAdmin = await TokenAdminAsync();

        await _trava.WaitAsync();

        try
        {
            if (_tokensPorPapel.TryGetValue(papel, out var existente))
                return existente;

            var usuario = await RegistrarClienteAsync();

            using var admin = CriarClienteComToken(tokenAdmin);

            using var concessao = await admin.PostAsync(
                $"/api/v1/admin/usuarios/{usuario.Id}/roles/{papel}", content: null);

            await GarantirSucessoAsync(concessao, $"conceder o papel {papel}");

            var token = await LogarAsync(usuario.Email, SenhaPadrao);
            _tokensPorPapel[papel] = token;

            return token;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Cadastra um cliente NOVO (papel "cliente") e devolve o token dele.</summary>
    public async Task<UsuarioDeTeste> RegistrarClienteAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];
        var email = $"cliente.{sufixo}@testes.glorific.art";

        using var cliente = CriarCliente();

        using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            senha = SenhaPadrao,
            nomeCompleto = "Cliente De Teste",
            aceitaMarketing = false
        });

        await GarantirSucessoAsync(resposta, "cadastrar o cliente de teste");

        var corpo = await LerJsonAsync(resposta);
        var usuario = corpo.GetProperty("usuario");

        return new UsuarioDeTeste(
            usuario.GetProperty("id").GetInt32(),
            usuario.GetProperty("uuid").GetString() ?? string.Empty,
            email,
            corpo.GetProperty("accessToken").GetString() ?? string.Empty);
    }

    private async Task<string> LogarAsync(string email, string senha)
    {
        using var cliente = CriarCliente();

        using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha });

        await GarantirSucessoAsync(resposta, $"logar como {email}");

        var corpo = await LerJsonAsync(resposta);

        return corpo.GetProperty("accessToken").GetString()
               ?? throw new InvalidOperationException("Login sem accessToken no corpo.");
    }

    // ------------------------------------------------------------------
    // Massa de catalogo
    // ------------------------------------------------------------------

    /// <summary>
    /// Cria um produto novo e gera a grade dele com o estoque pedido.
    ///
    /// Sempre um produto NOVO por chamada (SKU e nome com sufixo aleatorio): teste que reaproveita
    /// produto de outro teste passa a depender de ordem de execucao.
    /// </summary>
    public async Task<ProdutoDeTeste> CriarProdutoComGradeAsync(
        int estoqueInicial,
        int quantidadeTamanhos = 1,
        int quantidadeCores = 1)
    {
        using var admin = await CriarClienteAdminAsync();

        var idCategoria = await GarantirCategoriaAsync(admin);
        var sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        using var criacao = await admin.PostAsJsonAsync("/api/v1/admin/produtos", new
        {
            nome = $"Peca de teste {sufixo}",
            skuBase = $"TST-{sufixo}",
            idCategoria,
            precoBaseCentavos = 19900,
            descricao = "Produto criado pelo teste de integracao."
        });

        await GarantirSucessoAsync(criacao, "criar o produto de teste");

        var produto = await LerJsonAsync(criacao);
        var idProduto = produto.GetProperty("id").GetInt32();

        var tamanhos = await IdsTamanhosAsync();
        var cores = await IdsCoresAsync();

        using var grade = await admin.PostAsJsonAsync(
            $"/api/v1/admin/produtos/{idProduto}/variacoes/gerar-grade",
            new
            {
                idsTamanhos = tamanhos.Take(quantidadeTamanhos).ToArray(),
                idsCores = cores.Take(quantidadeCores).ToArray(),
                pesoGramas = 300,
                alturaCm = 5,
                larguraCm = 20,
                comprimentoCm = 30,
                precoCentavos = 19900,
                ativo = true,
                quantidadeInicial = estoqueInicial,
                quantidadeMinima = 0
            });

        await GarantirSucessoAsync(grade, "gerar a grade do produto de teste");

        var corpoGrade = await LerJsonAsync(grade);

        var variacoes = corpoGrade
            .GetProperty("variacoes")
            .EnumerateArray()
            .Select(v => new VariacaoDeTeste(
                v.GetProperty("id").GetInt32(),
                v.GetProperty("sku").GetString() ?? string.Empty,
                v.GetProperty("codigoTamanho").GetString() ?? string.Empty,
                v.GetProperty("nomeCor").GetString() ?? string.Empty,
                v.GetProperty("quantidadeDisponivel").GetInt32()))
            .ToArray();

        return new ProdutoDeTeste(
            idProduto,
            produto.GetProperty("nome").GetString() ?? string.Empty,
            produto.GetProperty("slug").GetString() ?? string.Empty,
            variacoes);
    }

    /// <summary>Uma categoria por sessao: categoria e cadastro compartilhado e inerte no teste.</summary>
    private async Task<int> GarantirCategoriaAsync(HttpClient admin)
    {
        if (_idCategoria is not null)
            return _idCategoria.Value;

        using var resposta = await admin.PostAsJsonAsync("/api/v1/admin/categorias", new
        {
            nome = $"Categoria de testes {Guid.NewGuid().ToString("N")[..8]}",
            ordem = 1,
            habilitado = true
        });

        await GarantirSucessoAsync(resposta, "criar a categoria de teste");

        var corpo = await LerJsonAsync(resposta);
        _idCategoria = corpo.GetProperty("id").GetInt32();

        return _idCategoria.Value;
    }

    private async Task<IReadOnlyList<int>> IdsTamanhosAsync() =>
        _idsTamanhos ??= await IdsDeAsync("/api/v1/tamanhos");

    private async Task<IReadOnlyList<int>> IdsCoresAsync() =>
        _idsCores ??= await IdsDeAsync("/api/v1/cores");

    private async Task<IReadOnlyList<int>> IdsDeAsync(string rota)
    {
        using var cliente = CriarCliente();
        using var resposta = await cliente.GetAsync(rota);

        await GarantirSucessoAsync(resposta, $"listar {rota}");

        var corpo = await LerJsonAsync(resposta);

        var ids = corpo.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();

        if (ids.Length == 0)
            throw new InvalidOperationException($"{rota} voltou vazia; o seed inicial nao rodou.");

        return ids;
    }

    // ------------------------------------------------------------------
    // Leitura de resposta
    // ------------------------------------------------------------------

    /// <summary>
    /// Corpo JSON como <see cref="JsonElement"/> independente do <see cref="JsonDocument"/>.
    ///
    /// Clonado de proposito: sem o Clone o elemento aponta para o buffer do documento, que ja foi
    /// devolvido ao pool quando o teste for ler — e o valor lido vira lixo, de forma intermitente.
    /// </summary>
    public static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException(
                $"Resposta {(int)resposta.StatusCode} de {resposta.RequestMessage?.RequestUri} veio com corpo VAZIO.");
        }

        using var documento = JsonDocument.Parse(texto);

        return documento.RootElement.Clone();
    }

    /// <summary>
    /// Massa de teste que falha ao ser montada precisa gritar COM O CORPO da resposta.
    ///
    /// Sem o corpo aqui, um 400 de validacao no cadastro do produto aparece la na frente como
    /// "IndexOutOfRange em Variacoes[0]" e o tempo vai embora procurando no lugar errado.
    /// </summary>
    private static async Task GarantirSucessoAsync(HttpResponseMessage resposta, string operacao)
    {
        if (resposta.IsSuccessStatusCode)
            return;

        var corpo = await resposta.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Falha ao {operacao}: {(int)resposta.StatusCode} {resposta.StatusCode}. Corpo: {corpo}");
    }

    // ------------------------------------------------------------------
    // Configuracao publicada no ambiente do processo de teste
    // ------------------------------------------------------------------

    private static void PublicarConfiguracaoNoAmbiente(string conexao)
    {
        var valores = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ASPNETCORE_ENVIRONMENT"] = Ambiente,
            ["DOTNET_ENVIRONMENT"] = Ambiente,

            ["ConnectionStrings__DefaultConnection"] = conexao,

            // O boot aplica migration e seed no banco do container. E o que garante que o teste
            // exercita o MESMO caminho de inicializacao do deploy, incluindo o advisory lock.
            ["Boot__AplicarMigrations"] = "true",

            ["Jwt__Key"] = ChaveJwt,
            ["Jwt__Issuer"] = "https://testes.glorific.art",
            ["Jwt__Audience"] = "https://testes.glorific.art",
            ["Jwt__AccessTokenMinutos"] = "60",
            ["Jwt__RefreshTokenDias"] = "30",
            ["Jwt__ClockSkewSegundos"] = "30",

            ["App__PublicBaseUrl"] = "http://localhost",
            ["App__LojaBaseUrl"] = "http://localhost",
            ["App__NomeLoja"] = "Glorific (testes)",

            // Fora de Development o boot exige estes tres segredos. Sao valores falsos de
            // proposito: nenhum teste desta frente fala com integracao externa.
            ["Google__ClientId"] = "cliente-de-teste.apps.googleusercontent.com",
            ["InfinitePay__Handle"] = "glorific-testes",
            ["MelhorEnvio__ApiKey"] = "chave-melhor-envio-de-teste",

            // Porta 9 (discard): qualquer chamada externa que escapar falha na hora, em vez de
            // sair para a internet a partir da suite de teste.
            ["InfinitePay__BaseUrl"] = "http://localhost:9",
            ["MelhorEnvio__BaseUrl"] = "http://localhost:9",

            ["Frete__CepOrigem"] = "80010000",
            ["Frete__Remetente__Nome"] = "Glorific Testes",
            ["Frete__Remetente__Documento"] = "00000000000000",
            ["Frete__Remetente__Logradouro"] = "Rua de Teste",
            ["Frete__Remetente__Numero"] = "100",
            ["Frete__Remetente__Bairro"] = "Centro",
            ["Frete__Remetente__Cidade"] = "Curitiba",
            ["Frete__Remetente__Uf"] = "PR",

            // O rate limit por IP e por processo: na suite TODAS as requisicoes compartilham a
            // mesma particao ("desconhecido", porque nao ha IP remoto no TestServer). Com o
            // limite de producao, a suite comecaria a receber 429 por conta propria.
            ["RateLimit__AuthPorMinuto"] = "100000",
            ["RateLimit__FretePorMinuto"] = "100000",
            ["RateLimit__ConsultaPorMinuto"] = "100000",

            // Mesmas variaveis do deploy: o admin do teste nasce pelo SeedAdmin, nao por um
            // atalho que so existe em teste.
            [SeedAdmin.VariavelEmail] = EmailAdmin,
            [SeedAdmin.VariavelSenha] = SenhaAdmin
        };

        foreach (var (chave, valor) in valores)
            Environment.SetEnvironmentVariable(chave, valor);
    }
}

/// <summary>Usuario cadastrado pelo teste, com o token ja emitido.</summary>
public sealed record UsuarioDeTeste(int Id, string Uuid, string Email, string Token);

/// <summary>Uma variacao (SKU vendavel) da grade gerada pelo teste.</summary>
public sealed record VariacaoDeTeste(int Id, string Sku, string CodigoTamanho, string NomeCor, int Disponivel);

/// <summary>Produto criado pelo teste, com a grade ja gerada.</summary>
public sealed record ProdutoDeTeste(int Id, string Nome, string Slug, IReadOnlyList<VariacaoDeTeste> Variacoes)
{
    public VariacaoDeTeste PrimeiraVariacao => Variacoes[0];
}

/// <summary>
/// UM container e UMA API para a sessao inteira. Container por classe de teste multiplicaria o
/// tempo de suite por dezenas sem provar nada a mais.
///
/// DisableParallelization: a configuracao da API viaja por VARIAVEL DE AMBIENTE (ver o cabecalho
/// da ApiFixture), que e estado do PROCESSO. Se outra colecao subisse a propria API em paralelo,
/// as duas disputariam a mesma ConnectionStrings__DefaultConnection e uma delas apontaria para o
/// banco da outra — falha intermitente, do tipo que se culpa o "teste flaky" e nao a causa.
/// </summary>
[CollectionDefinition(ColecaoApi.Nome, DisableParallelization = true)]
public sealed class ColecaoApi : ICollectionFixture<ApiFixture>
{
    public const string Nome = "api-http-testcontainers";
}
