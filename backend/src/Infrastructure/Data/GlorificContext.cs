using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Entities.Config;
using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Entities.Social;
using Glorific.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

// A pasta Carrinho e ao mesmo tempo namespace e nome de entidade; sem os alias o compilador
// resolve "Carrinho" como namespace.
using CarrinhoEntity = Glorific.Domain.Entities.Carrinho.Carrinho;
using CarrinhoItemEntity = Glorific.Domain.Entities.Carrinho.CarrinhoItem;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// DbContext unico da aplicacao e implementacao de IUnitOfWork.
///
/// Duas decisoes que valem o comentario:
/// (a) OnModelCreating so aplica as configurations do assembly — nenhum mapeamento inline e
///     nenhum HasData. Seed vive em seeder idempotente, nao no modelo: HasData entra no
///     snapshot e vira migration pendente a cada ajuste de dado.
/// (b) Quem salva e o caso de uso, nunca o repositorio. Por isso SaveChangesAsync e a
///     transacao explicita moram aqui, atras de IUnitOfWork, e o Application nao precisa
///     enxergar EF para dizer "isto tudo acontece junto".
/// </summary>
public class GlorificContext : DbContext, IUnitOfWork
{
    private readonly IClock _clock;

    public GlorificContext(DbContextOptions<GlorificContext> options, IClock clock)
        : base(options)
    {
        _clock = clock;
    }

    // ---------- Catalogo ----------
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Colecao> Colecoes => Set<Colecao>();
    public DbSet<Cor> Cores => Set<Cor>();
    public DbSet<LogProduto> LogsProdutos => Set<LogProduto>();
    public DbSet<Midia> Midias => Set<Midia>();
    public DbSet<MidiaProduto> MidiasProdutos => Set<MidiaProduto>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ProdutoColecao> ProdutosColecoes => Set<ProdutoColecao>();
    public DbSet<ProdutoVariacao> ProdutoVariacoes => Set<ProdutoVariacao>();
    public DbSet<TabelaMedidas> TabelasMedidas => Set<TabelaMedidas>();
    public DbSet<TabelaMedidasLinha> TabelasMedidasLinhas => Set<TabelaMedidasLinha>();
    public DbSet<Tamanho> Tamanhos => Set<Tamanho>();

    // ---------- Estoque ----------
    public DbSet<EstoqueVariacao> EstoquesVariacoes => Set<EstoqueVariacao>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<MovimentoEstoque> MovimentosEstoque => Set<MovimentoEstoque>();

    // ---------- Carrinho ----------
    public DbSet<CarrinhoEntity> Carrinhos => Set<CarrinhoEntity>();
    public DbSet<CarrinhoItemEntity> CarrinhoItens => Set<CarrinhoItemEntity>();

    // ---------- Clientes ----------
    public DbSet<Endereco> Enderecos => Set<Endereco>();
    public DbSet<ListaDesejoItem> ListaDesejoItens => Set<ListaDesejoItem>();

    // ---------- Identidade ----------
    public DbSet<LoginExterno> LoginsExternos => Set<LoginExterno>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioRole> UsuariosRoles => Set<UsuarioRole>();

    // ---------- Pedidos ----------
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<EnvioEvento> EnviosEventos => Set<EnvioEvento>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<PagamentoEvento> PagamentosEventos => Set<PagamentoEvento>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoHistorico> PedidosHistorico => Set<PedidoHistorico>();
    public DbSet<PedidoItem> PedidoItens => Set<PedidoItem>();

    // ---------- Promocoes ----------
    public DbSet<Cupom> Cupons => Set<Cupom>();
    public DbSet<CupomUso> CuponsUsos => Set<CupomUso>();

    // ---------- Social ----------
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<AvaliacaoMidia> AvaliacoesMidias => Set<AvaliacaoMidia>();

    // ---------- Config ----------
    public DbSet<AppSecret> AppSecrets => Set<AppSecret>();
    public DbSet<ConfiguracaoLoja> ConfiguracoesLoja => Set<ConfiguracaoLoja>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GlorificContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // ------------------------------------------------------------------
    // IUnitOfWork
    // ------------------------------------------------------------------

    /// <summary>
    /// Transacao explicita para o que precisa ser atomico entre agregados — o checkout
    /// reserva estoque, consome cupom e cria pedido, e ou tudo acontece ou nada acontece.
    /// </summary>
    public async Task<IDbTransacao> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transacao = await Database.BeginTransactionAsync(cancellationToken);
        return new TransacaoEf(transacao);
    }

    // ------------------------------------------------------------------
    // Auditoria automatica
    // ------------------------------------------------------------------

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AplicarAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Carimbo de criacao/alteracao vindo do IClock, nunca de DateTime.UtcNow espalhado pelos
    /// services. DataCriacao e marcada como nao modificada no update: sem isso, um update que
    /// carrega a entidade desanexada com DataCriacao default reescreve a data original.
    /// </summary>
    private void AplicarAuditoria()
    {
        var agora = _clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.DataCriacao = agora;
                    entry.Entity.DataAlteracao = null;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.DataCriacao)).IsModified = false;
                    entry.Entity.DataAlteracao = agora;
                    break;
            }
        }
    }
}
