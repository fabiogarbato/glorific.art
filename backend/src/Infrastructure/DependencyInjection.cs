using Glorific.Application.Ports;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Glorific.Infrastructure.Repositories;
using Glorific.Infrastructure.Storage;
using Glorific.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Glorific.Infrastructure;

/// <summary>
/// Composicao da camada de infraestrutura. E o unico ponto onde a API aprende que existe
/// Postgres — o Program.cs chama AddInfrastructure e nao referencia EF em lugar nenhum.
/// </summary>
public static class DependencyInjection
{
    public const string NomeConexaoPadrao = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // CRITICO, e antes de qualquer coisa: espelha o GlorificContextFactory.
        // Sem o switch, o Npgsql mapeia DateTime para timestamptz enquanto as configurations
        // declaram "timestamp without time zone" — runtime e design-time divergem, a migration
        // nasce diferente do modelo e o PendingModelChangesWarning derruba a API no boot.
        // Alem disso, gravar um DateTime com Kind=Utc (que e o que o IClock devolve) em coluna
        // timestamp sem o switch lanca excecao na primeira insercao.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var conexao = configuration.GetConnectionString(NomeConexaoPadrao);

        if (string.IsNullOrWhiteSpace(conexao))
            throw new InvalidOperationException(
                $"Connection string '{NomeConexaoPadrao}' nao configurada. " +
                "Defina ConnectionStrings__DefaultConnection no ambiente.");

        services.AddDbContext<GlorificContext>(opcoes =>
            opcoes.UseNpgsql(conexao, npg =>
                npg.MigrationsAssembly(typeof(GlorificContext).Assembly.GetName().Name)));

        // Relogio unico do processo. Singleton porque nao tem estado e e lido em toda regra de
        // expiracao — carrinho, token, cupom, backoff de envio.
        services.AddSingleton<IClock, RelogioSistema>();

        // O proprio DbContext e a unidade de trabalho. Resolvido pelo mesmo escopo para o
        // SaveChanges do caso de uso enxergar exatamente o que os repositorios rastrearam —
        // registrar uma instancia separada faria o commit salvar um contexto vazio.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GlorificContext>());

        // Acesso ao catalogo ignorando o filtro global de soft delete. Sem isto o painel
        // administrativo nao consegue LISTAR nem REATIVAR o que ele mesmo desativou, porque a
        // entidade simplesmente nao volta da consulta.
        services.AddScoped<IConsultaCatalogoSemFiltro, ConsultaCatalogoSemFiltroEf>();

        // Armazenamento de imagem em disco (wwwroot/media). E o suficiente para o MVP; a porta
        // permite trocar por Cloudinary/S3 depois sem tocar em nenhum servico da Application.
        services.AddOptions<ArmazenamentoLocalOptions>()
            .Bind(configuration.GetSection(ArmazenamentoLocalOptions.SectionName));

        services.AddScoped<IImageStorage, ArmazenamentoLocalImagem>();

        return services.AddRepositorios();
    }

    /// <summary>
    /// Todos Scoped: cada repositorio compartilha o DbContext da requisicao. Singleton aqui
    /// vazaria o ChangeTracker de um cliente para outro.
    /// </summary>
    private static IServiceCollection AddRepositorios(this IServiceCollection services)
    {
        // Repositorio generico aberto: agregados-filhos que nao tem contrato proprio no Domain
        // (MidiaProduto, ProdutoColecao, LogProduto, TabelaMedidasLinha) precisam de UM ponto de
        // escrita, e criar um repositorio dedicado para cada tabela de juncao so aumentaria a
        // superficie sem acrescentar regra. Continua valendo a regra dura: nada aqui salva.
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

        services.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
        services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IColecaoRepository, ColecaoRepository>();
        services.AddScoped<IConfiguracaoLojaRepository, ConfiguracaoLojaRepository>();
        services.AddScoped<ICorRepository, CorRepository>();
        services.AddScoped<ICupomRepository, CupomRepository>();
        services.AddScoped<IEnderecoRepository, EnderecoRepository>();
        services.AddScoped<IEnvioRepository, EnvioRepository>();
        services.AddScoped<IEstoqueRepository, EstoqueRepository>();
        services.AddScoped<IListaDesejoRepository, ListaDesejoRepository>();
        services.AddScoped<IMidiaRepository, MidiaRepository>();
        services.AddScoped<IMovimentoEstoqueRepository, MovimentoEstoqueRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IProdutoVariacaoRepository, ProdutoVariacaoRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITabelaMedidasRepository, TabelaMedidasRepository>();
        services.AddScoped<ITamanhoRepository, TamanhoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        return services;
    }
}
