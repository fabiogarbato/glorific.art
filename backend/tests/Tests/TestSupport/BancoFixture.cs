using Glorific.Domain.Interfaces;
using Glorific.Infrastructure;
using Glorific.Infrastructure.Data;
using Glorific.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Glorific.Tests.TestSupport;

/// <summary>
/// UM container Postgres REAL por sessao de teste, compartilhado via ICollectionFixture.
///
/// POR QUE POSTGRES DE VERDADE E NAO SQLITE/InMemory
/// O que esta suite precisa provar so existe no Postgres: jsonb como tipo (e nao texto),
/// o xmin como token de concorrencia otimista, "timestamp without time zone" convivendo com
/// DateTime Kind=Utc, indice unico PARCIAL com clausula WHERE, CHECK constraint recusando
/// UPDATE e ON DELETE declarado na FK. SQLite aceita calado todos esses casos e o teste passaria
/// verde enquanto producao quebra — foi exatamente esse o buraco do repo de referencia.
/// Alem disso, os testes de concorrencia dependem de MVCC real com UPDATE condicional atomico:
/// sem banco de verdade, "20 reservas concorrentes consomem 5" nao prova absolutamente nada.
///
/// ESTRATEGIA DE LIMPEZA ENTRE TESTES: TRUNCATE + RE-SEED (decisao documentada)
/// A alternativa classica — abrir uma transacao no inicio do teste e dar rollback no fim — foi
/// DESCARTADA de proposito. Ela obriga todo o teste a viver numa unica conexao, e as tarefas
/// concorrentes deste projeto precisam de conexoes SEPARADAS, cada uma com sua propria
/// transacao, para que o UPDATE condicional dispute a linha de verdade. Dentro de uma transacao
/// unica compartilhada nao ha disputa nenhuma e o teste de oversell viraria teatro.
///
/// Entao <see cref="LimparAsync"/> faz:
///   1. TRUNCATE de TODAS as tabelas de dado do schema public (menos __EFMigrationsHistory),
///      em UMA instrucao com RESTART IDENTITY CASCADE. Uma instrucao so resolve a ordem das FKs
///      sem lista topologica a mao; RESTART IDENTITY zera as sequences, entao os ids nascem
///      iguais em toda execucao e nenhum teste depende de id vindo do teste anterior.
///   2. Re-execucao do <see cref="SeedInicial"/>, que devolve as tabelas de REFERENCIA
///      (roles, movimentos_estoque, configuracao da loja, grade de tamanhos, cores base).
///      Ou seja: o efeito liquido e "truncar dado, manter referencia", mas sem lista de excecao
///      no TRUNCATE — que seria fragil, porque cores e categorias apontam para midias e um
///      TRUNCATE ... CASCADE em midias levaria as tabelas de referencia junto.
///
/// Como o Postgres e um so para a sessao inteira, TODA classe que usa este fixture precisa
/// entrar em <see cref="BancoCollection"/>: xUnit serializa os testes de uma mesma collection,
/// e e isso que impede um teste truncar a tabela debaixo do outro.
/// </summary>
public sealed class BancoFixture : IAsyncLifetime
{
    /// <summary>
    /// Espelha o Program.cs e o GlorificContextFactory. Sem o switch, o Npgsql mapeia DateTime
    /// para timestamptz enquanto as configurations declaram "timestamp without time zone" e a
    /// primeira insercao de um DateTime com Kind=Utc (que e o que o IClock devolve) estoura.
    /// Fica em construtor estatico porque precisa valer ANTES de qualquer conexao ser aberta.
    /// </summary>
    static BancoFixture()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    // Imagem no construtor: o construtor sem parametros do PostgreSqlBuilder esta obsoleto
    // (Testcontainers 4.14+). Mesma imagem do docker-compose.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("glorific_testes")
        .WithUsername("glorific")
        .WithPassword("glorific")
        .Build();

    private ServiceProvider? _raiz;

    /// <summary>Nomes ja escapados (quote_ident) das tabelas que o TRUNCATE limpa.</summary>
    private string[] _tabelas = [];

    /// <summary>Connection string do container. So tem valor depois do InitializeAsync.</summary>
    public string StringConexao { get; private set; } = string.Empty;

    /// <summary>
    /// Relogio FIXO da sessao. Substitui o RelogioSistema no container de DI para que
    /// DataCriacao/DataAlteracao carimbadas pela auditoria do DbContext sejam deterministicas —
    /// teste que compara data com DateTime.UtcNow e teste que falha sozinho de madrugada.
    /// </summary>
    public RelogioDeTeste Relogio { get; } = new(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        StringConexao = _container.GetConnectionString();

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.NomeConexaoPadrao}"] = StringConexao
            })
            .Build();

        var servicos = new ServiceCollection();

        // A MESMA composicao que a API usa em producao. Testar contra um container montado a mao
        // provaria os repositorios e nao o wiring; aqui os dois entram no teste.
        servicos.AddInfrastructure(configuracao);

        // Registrado DEPOIS do AddInfrastructure de proposito: no ServiceCollection a ultima
        // registracao vence, entao isto troca o RelogioSistema pelo relogio fixo sem precisar
        // mexer na composicao de producao.
        servicos.AddSingleton<IClock>(Relogio);

        _raiz = servicos.BuildServiceProvider();

        await using (var contexto = CriarContexto())
        {
            // O MigrationRunner de producao, e nao um EnsureCreated: EnsureCreated monta o schema
            // a partir do modelo e PULA as migrations, entao qualquer divergencia entre migration
            // e modelo (o postmortem do repo de referencia) passaria despercebida justamente aqui.
            await MigrationRunner.AplicarAsync(contexto, LoggerSilencioso.Instancia);
            await SeedInicial.ExecutarAsync(contexto, LoggerSilencioso.Instancia);
        }

        _tabelas = (await ConsultarColunaAsync(
                """
                SELECT quote_ident(table_name)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name <> '__EFMigrationsHistory'
                ORDER BY table_name
                """))
            .Select(nome => "public." + nome)
            .ToArray();
    }

    public async Task DisposeAsync()
    {
        if (_raiz is not null)
            await _raiz.DisposeAsync();

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Contexto novo, com conexao propria. E o que os testes de concorrencia usam quando
    /// precisam de N contextos independentes — DbContext nao e thread-safe e compartilhar um
    /// entre tarefas paralelas produz falha intermitente que nao tem nada a ver com a regra
    /// sob teste.
    /// </summary>
    public GlorificContext CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<GlorificContext>()
            .UseNpgsql(
                StringConexao,
                npg => npg.MigrationsAssembly(typeof(GlorificContext).Assembly.GetName().Name))
            .Options;

        return new GlorificContext(opcoes, Relogio);
    }

    /// <summary>
    /// Escopo de DI equivalente ao de uma requisicao HTTP: um GlorificContext, e todos os
    /// repositorios daquele escopo compartilhando esse contexto — exatamente como em producao.
    /// Cada tarefa concorrente abre o SEU escopo.
    /// </summary>
    public IServiceScope CriarEscopo() =>
        (_raiz ?? throw new InvalidOperationException("Fixture nao inicializado.")).CreateScope();

    /// <summary>
    /// Zera o banco e devolve o dado de referencia. Chame no InitializeAsync de cada classe de
    /// teste (ou no inicio de cada teste): e o que torna a suite independente de ordem.
    /// </summary>
    public async Task LimparAsync()
    {
        if (_tabelas.Length == 0)
            throw new InvalidOperationException("Fixture nao inicializado.");

        // Uma instrucao so: o Postgres resolve as FKs entre as tabelas listadas sem exigir ordem.
        // CASCADE aqui e apenas cinto de seguranca — todas as tabelas ja estao na lista.
        await ExecutarAsync(
            $"TRUNCATE TABLE {string.Join(", ", _tabelas)} RESTART IDENTITY CASCADE");

        await using var contexto = CriarContexto();
        await SeedInicial.ExecutarAsync(contexto, LoggerSilencioso.Instancia);
    }

    // ------------------------------------------------------------------
    // SQL cru — usado pelos testes de schema e pelos que precisam furar o EF de proposito
    // ------------------------------------------------------------------

    /// <summary>
    /// Executa SQL sem passar pelo EF. Proposital: o teste do CHECK precisa de um UPDATE que o
    /// repositorio jamais emitiria, para provar que a rede de seguranca esta no BANCO e nao no
    /// WHERE do C#. A PostgresException sobe crua, sem embrulho de DbUpdateException.
    /// </summary>
    public async Task<int> ExecutarAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var conexao = new NpgsqlConnection(StringConexao);
        await conexao.OpenAsync(cancellationToken);

        await using var comando = new NpgsqlCommand(sql, conexao);
        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Consulta arbitraria; cada linha vem como array de string (null preservado).</summary>
    public async Task<IReadOnlyList<string?[]>> ConsultarAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var conexao = new NpgsqlConnection(StringConexao);
        await conexao.OpenAsync(cancellationToken);

        await using var comando = new NpgsqlCommand(sql, conexao);
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);

        var linhas = new List<string?[]>();

        while (await leitor.ReadAsync(cancellationToken))
        {
            var linha = new string?[leitor.FieldCount];

            for (var coluna = 0; coluna < leitor.FieldCount; coluna++)
            {
                linha[coluna] = await leitor.IsDBNullAsync(coluna, cancellationToken)
                    ? null
                    : leitor.GetValue(coluna).ToString();
            }

            linhas.Add(linha);
        }

        return linhas;
    }

    /// <summary>Atalho para consulta de uma coluna so.</summary>
    public async Task<IReadOnlyList<string>> ConsultarColunaAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        var linhas = await ConsultarAsync(sql, cancellationToken);
        return linhas.Select(linha => linha[0] ?? string.Empty).ToList();
    }

    // ------------------------------------------------------------------
    // Test doubles a mao — o projeto nao usa biblioteca de mock
    // ------------------------------------------------------------------

    /// <summary>
    /// IClock controlavel. Comeca fixo e so anda quando o teste mandar, o que e o unico jeito
    /// de exercitar vigencia de cupom, expiracao de carrinho e backoff de envio sem esperar.
    /// </summary>
    public sealed class RelogioDeTeste : IClock
    {
        private long _ticks;

        public RelogioDeTeste(DateTime inicio) => _ticks = inicio.Ticks;

        public DateTime UtcNow => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);

        public void Definir(DateTime instante) =>
            Interlocked.Exchange(ref _ticks, instante.ToUniversalTime().Ticks);

        public void Avancar(TimeSpan intervalo) =>
            Interlocked.Add(ref _ticks, intervalo.Ticks);
    }

    /// <summary>ILogger que engole tudo. O MigrationRunner e o SeedInicial exigem um.</summary>
    private sealed class LoggerSilencioso : ILogger
    {
        public static readonly LoggerSilencioso Instancia = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}

/// <summary>
/// Amarra todas as classes de teste que tocam o banco ao MESMO container.
///
/// Duas consequencias, as duas desejadas: sobe um Postgres so para a sessao inteira (o custo de
/// subir um por classe seria proibitivo) e o xUnit executa essas classes em SERIE, o que e o que
/// permite o TRUNCATE entre testes sem um teste zerar a tabela debaixo do outro.
/// </summary>
[CollectionDefinition(BancoCollection.Nome)]
public sealed class BancoCollection : ICollectionFixture<BancoFixture>
{
    public const string Nome = "banco-postgres";
}
