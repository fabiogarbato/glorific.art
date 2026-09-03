using Glorific.Api.Common;
using Glorific.Api.Configuration;
using Glorific.Api.Middleware;
using Glorific.Application;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Infrastructure;
using Glorific.Infrastructure.Data;
using Glorific.Infrastructure.Integrations.MelhorEnvio;
using Glorific.Infrastructure.Seeding;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

#region Compatibilidade Npgsql (PRIMEIRA linha executavel)

// CRITICO, antes de qualquer outra coisa e espelhado no GlorificContextFactory.
// Sem o switch, o Npgsql mapeia DateTime para timestamptz enquanto as configurations declaram
// "timestamp without time zone": design-time e runtime divergem, a migration nasce diferente do
// modelo e o PendingModelChangesWarning derruba a API no boot. Alem disso, gravar um DateTime
// com Kind=Utc (que e o que o IClock devolve) em coluna timestamp sem o switch lanca na
// primeira insercao. Postmortem documentado do repo de referencia.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

#endregion

var builder = WebApplication.CreateBuilder(args);
var ehDesenvolvimento = builder.Environment.IsDevelopment();

#region Logging

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opcoes =>
{
    opcoes.SingleLine = true;
    opcoes.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    // Timestamp em UTC: o container roda em America/Sao_Paulo, o agregador de log em UTC, e
    // correlacionar dois relogios diferentes durante um incidente custa caro.
    opcoes.UseUtcTimestamp = true;
});

#endregion

#region CORS

builder.Services.AddCorsConfigurado(builder.Configuration, builder.Environment);

#endregion

#region ForwardedHeaders

builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Listas limpas porque o IP do proxy dentro da rede do Docker/Swarm nao e conhecido no
    // build. O custo e real: quem alcancar o Kestrel DIRETAMENTE pode forjar X-Forwarded-For e
    // escapar do rate limit por IP. A mitigacao e de rede — o container so pode ser exposto
    // atras do proxy reverso, nunca com porta publicada direto.
    opcoes.KnownIPNetworks.Clear();
    opcoes.KnownProxies.Clear();
});

#endregion

#region Controllers + ProblemDetails

builder.Services.AddProblemDetails();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opcoes =>
    {
        // O [ApiController] devolve ValidationProblemDetails por padrao — um QUARTO formato de
        // erro convivendo com o do middleware. Aqui a validacao de ModelState sai no MESMO
        // envelope { statusCode, error, traceId, errors }, com o detalhe por campo em "errors".
        opcoes.InvalidModelStateResponseFactory = contexto =>
        {
            var erros = contexto.ModelState
                .Where(par => par.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    par => par.Key,
                    par => par.Value!.Errors
                        .Select(erro => string.IsNullOrWhiteSpace(erro.ErrorMessage)
                            ? "Valor invalido."
                            : erro.ErrorMessage)
                        .ToArray(),
                    StringComparer.Ordinal);

            var envelope = RespostaErro.Criar(
                contexto.HttpContext,
                StatusCodes.Status400BadRequest,
                "Requisicao invalida. Verifique os campos informados.",
                erros);

            return new ObjectResult(envelope) { StatusCode = StatusCodes.Status400BadRequest };
        };
    });

#endregion

#region Mapster

// O Scan + Compile acontece dentro do AddApplication (adiante). Aqui so entra a composicao:
// a config global vira singleton e o IMapper vira ServiceMapper, que resolve dependencia de
// DI dentro do mapeamento — necessario para MapWith que precisa de servico.
builder.Services.AddSingleton(TypeAdapterConfig.GlobalSettings);
builder.Services.AddScoped<IMapper, ServiceMapper>();

#endregion

#region RequiredSecret (fail-fast de segredos)

// Segredo ausente derruba o boot AQUI, e nao no primeiro cliente que tentar usar a integracao.
// O valor retornado e o que deve ser usado adiante: ele ja vem com Trim aplicado.
var chaveJwt = RequiredSecret.Require(
    builder.Configuration, "Jwt:Key", "Jwt__Key", tamanhoMinimo: 32);

RequiredSecret.Require(
    builder.Configuration, "ConnectionStrings:DefaultConnection", "ConnectionStrings__DefaultConnection");

// Integracoes externas sao exigidas fora de Development: um desenvolvedor precisa conseguir
// subir a API local para mexer no catalogo sem ter credencial de gateway de pagamento.
RequiredSecret.RequireSe(!ehDesenvolvimento, builder.Configuration, "Google:ClientId", "Google__ClientId");
RequiredSecret.RequireSe(!ehDesenvolvimento, builder.Configuration, "InfinitePay:Handle", "InfinitePay__Handle");
RequiredSecret.RequireSe(!ehDesenvolvimento, builder.Configuration, "MelhorEnvio:ApiKey", "MelhorEnvio__ApiKey");

#endregion

#region DbContext, repositorios e relogio

builder.Services.AddInfrastructure(builder.Configuration);

// Adaptadores da vertical de identidade: emissao de token JWT, validacao do id_token do Google e
// token de redefinicao de senha. Em arquivo proprio da Infrastructure para que varias frentes de
// trabalho registrem adaptadores em paralelo sem disputar o mesmo ponto de merge.
builder.Services.AddIdentidadeInfrastructure();

// Materializacao assincrona de IQueryable para a camada Application, que nao referencia EF.
// Fica no Program porque e a API que compoe as duas camadas.
builder.Services.AddSingleton<IConsultaAssincrona, ConsultaAssincronaEf>();

// Cache em memoria de processo. Hoje sustenta a cotacao de frete (2 min por CEP e assinatura de
// itens) e a configuracao da loja, lida em toda cotacao. Em memoria e nao distribuido porque a
// API roda em no unico por decisao de infraestrutura; com replica, cada uma teria o proprio
// cache e o efeito seria apenas uma cotacao a mais no parceiro, nunca dado incorreto.
builder.Services.AddMemoryCache();

#endregion

#region Swagger

builder.Services.AddSwaggerConfigurado();

#endregion

#region Application (servicos por convencao + Mapster scan)

builder.Services.AddApplication();

#endregion

#region Options bind

// Jwt e App sao exigidos em todo ambiente: sem eles nao ha token nem URL de callback.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// As demais secoes so sao validadas fora de Development, pelo mesmo motivo do RequiredSecret.
BindOpcoes<GoogleOptions>(GoogleOptions.SectionName);
BindOpcoes<InfinitePayOptions>(InfinitePayOptions.SectionName);
BindOpcoes<MelhorEnvioOptions>(MelhorEnvioOptions.SectionName);
BindOpcoes<FreteOptions>(FreteOptions.SectionName);

void BindOpcoes<TOpcoes>(string secao) where TOpcoes : class
{
    var construtor = builder.Services
        .AddOptions<TOpcoes>()
        .Bind(builder.Configuration.GetSection(secao));

    if (!ehDesenvolvimento)
        construtor.ValidateDataAnnotations().ValidateOnStart();
}

#endregion

#region HttpClients tipados

// Os clients sao registrados por NOME aqui e consumidos pelos adaptadores da Infrastructure.
// Quem adicionar um adaptador tipado troca para AddHttpClient<IPorta, Adaptador>(nome) mantendo
// a mesma configuracao de BaseAddress e timeout.
var melhorEnvio = builder.Configuration
    .GetSection(MelhorEnvioOptions.SectionName).Get<MelhorEnvioOptions>() ?? new MelhorEnvioOptions();

var infinitePay = builder.Configuration
    .GetSection(InfinitePayOptions.SectionName).Get<InfinitePayOptions>() ?? new InfinitePayOptions();

// Client TIPADO: quem resolve IMelhorEnvioClient recebe o adaptador ja com BaseAddress, timeout
// e o header X-Api-Key (posto pelo proprio adaptador, a partir das options). O nome continua
// registrado para o handler ficar identificavel no log e nas metricas do HttpClientFactory.
builder.Services.AddHttpClient<IMelhorEnvioClient, MelhorEnvioClient>(NomesHttpClient.MelhorEnvio, cliente =>
{
    cliente.BaseAddress = new Uri(melhorEnvio.BaseUrl);
    // Timeout explicito: o default do HttpClient e 100 s, e uma cotacao presa por 100 s segura
    // um worker de requisicao inteiro enquanto o cliente ja desistiu.
    cliente.Timeout = TimeSpan.FromSeconds(melhorEnvio.TimeoutSegundos);
});

builder.Services.AddHttpClient(NomesHttpClient.InfinitePay, cliente =>
{
    cliente.BaseAddress = new Uri(infinitePay.BaseUrl);
    cliente.Timeout = TimeSpan.FromSeconds(infinitePay.TimeoutSegundos);
});

builder.Services.AddHttpClient(NomesHttpClient.ViaCep, cliente =>
{
    cliente.BaseAddress = new Uri("https://viacep.com.br/");
    cliente.Timeout = TimeSpan.FromSeconds(10);
});

#endregion

#region Pagamento, envio e workers

// Adaptador da InfinitePay (IPaymentGateway) amarrado ao HttpClient nomeado acima, mais os dois
// workers: EnvioProcessor (etiquetas) e PagamentoProcessor (drena a fila de eventos e expira
// cobranca vencida, devolvendo a reserva de estoque). Ver PagamentoEnvioConfiguration: ambos sao
// single-instance por design. Ainda falta o CarrinhoAbandonadoWorker, que e so higiene de dados.
builder.Services.AddPagamentoEEnvio();

#endregion

#region Autenticacao e autorizacao

builder.Services.AddAutenticacao(builder.Configuration, chaveJwt);
builder.Services.AddAutorizacao();

#endregion

#region RateLimiter

builder.Services.AddRateLimiting(builder.Configuration);

#endregion

#region HealthChecks

builder.Services.AddHealthChecksConfigurados();

#endregion

var app = builder.Build();

app.LogarConfiguracaoCors();

#region Migrations e seeds

// Desligavel por configuracao para o teste de integracao controlar o proprio schema.
if (builder.Configuration.GetValue("Boot:AplicarMigrations", true))
{
    await using var escopoBoot = app.Services.CreateAsyncScope();

    var contextoBoot = escopoBoot.ServiceProvider.GetRequiredService<GlorificContext>();
    var loggerBoot = escopoBoot.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Boot");

    // Advisory lock do Postgres por dentro: com mais de uma replica, a segunda ESPERA em vez de
    // rodar o mesmo CREATE TABLE em paralelo.
    await MigrationRunner.AplicarAsync(contextoBoot, loggerBoot);

    // Idempotente: papeis, movimentos de estoque, configuracao da loja, grade e cores base.
    await SeedInicial.ExecutarAsync(contextoBoot, loggerBoot);

    // Admin inicial. DEPOIS do seed de referencia, que e quem cria a linha do papel "admin".
    // Sem ADMIN_EMAIL/ADMIN_SENHA no ambiente ele nao faz nada e apenas avisa no log — nao
    // existe senha padrao no codigo, porque credencial de fabrica sobrevive ao deploy e e o
    // primeiro alvo de qualquer varredura automatizada.
    await SeedAdmin.ExecutarAsync(
        contextoBoot,
        builder.Configuration,
        loggerBoot,
        escopoBoot.ServiceProvider.GetRequiredService<Glorific.Domain.Interfaces.IClock>());
}

#endregion

#region Pipeline

app.UseForwardedHeaders();

// Serve wwwroot/media, onde o ArmazenamentoLocalImagem grava as fotos do catalogo.
// Fica ANTES do UseAuthorization de proposito: a FallbackPolicy exige usuario autenticado, e
// foto de vitrine tem de abrir para visitante anonimo. Middleware de arquivo estatico e
// terminal, entao a requisicao de imagem nem chega na autorizacao.
app.UseStaticFiles();

// Swagger publica o mapa completo da API, incluindo as rotas administrativas. Fora de producao.
if (!app.Environment.IsProduction())
    app.UseSwaggerConfigurado();

app.UseRouting();

// UseCors ANTES do middleware de excecao, de proposito: a resposta de erro precisa sair com o
// header Access-Control-Allow-Origin. Sem isso o navegador esconde o 400/500 real atras de um
// "erro de CORS" generico e quem esta depurando perde a mensagem de verdade.
app.UseCors(CorsConfiguration.NomePolitica);

app.UseExceptionHandlingConfigurado();

// Depois do exception middleware para que a rejeicao 429 saia no envelope unico, e antes da
// autenticacao para que forca bruta de login seja barrada sem custo de validacao de token.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// AllowAnonymous explicito: a FallbackPolicy exige usuario autenticado, e um /health que
// responde 401 faz o orquestrador matar um container saudavel em loop.
app.MapHealthChecks("/health").AllowAnonymous();

#endregion

app.Run();

/// <summary>
/// Torna a classe gerada pelos top-level statements publica para o WebApplicationFactory dos
/// testes de integracao conseguir referencia-la.
/// </summary>
public partial class Program;
