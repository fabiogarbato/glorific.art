using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// A pasta Carrinho e ao mesmo tempo namespace e nome de entidade; sem os alias o compilador
// resolve "Carrinho" como namespace.
using CarrinhoEntity = Glorific.Domain.Entities.Carrinho.Carrinho;
using CarrinhoItemEntity = Glorific.Domain.Entities.Carrinho.CarrinhoItem;

namespace Glorific.Infrastructure.Repositories;

public sealed class CarrinhoRepository : BaseRepository<CarrinhoEntity>, ICarrinhoRepository
{
    public CarrinhoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>Carrinho aberto do usuario logado, com a tela inteira carregada.</summary>
    public Task<CarrinhoEntity?> ObterAbertoDoUsuarioAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        ConsultaCompleta().FirstOrDefaultAsync(
            c => c.IdUsuario == idUsuario && c.Status == StatusCarrinho.Aberto,
            cancellationToken);

    /// <summary>Carrinho do visitante anonimo, achado pelo cookie de sessao.</summary>
    public Task<CarrinhoEntity?> ObterAbertoPorSessaoAsync(
        string chaveSessao,
        CancellationToken cancellationToken = default) =>
        ConsultaCompleta().FirstOrDefaultAsync(
            c => c.ChaveSessao == chaveSessao && c.Status == StatusCarrinho.Aberto,
            cancellationToken);

    public Task<CarrinhoEntity?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default) =>
        ConsultaCompleta().FirstOrDefaultAsync(c => c.Uuid == uuid, cancellationToken);

    /// <summary>
    /// Rastreado: quem procura o item ja vai somar quantidade ou trocar o preco snapshot.
    /// IgnoreQueryFilters porque a variacao pode ter sido desativada depois de o item entrar —
    /// o item precisa ser encontrado para poder ser removido ou avisado ao cliente.
    /// </summary>
    public Task<CarrinhoItemEntity?> ObterItemAsync(
        int idCarrinho,
        int idVariacao,
        CancellationToken cancellationToken = default) =>
        Contexto.CarrinhoItens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                i => i.IdCarrinho == idCarrinho && i.IdVariacao == idVariacao,
                cancellationToken);

    public async Task AdicionarItemAsync(CarrinhoItemEntity item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await Contexto.CarrinhoItens.AddAsync(item, cancellationToken);
    }

    public void RemoverItem(CarrinhoItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Contexto.CarrinhoItens.Remove(item);
    }

    /// <summary>
    /// Fila do worker de abandono. O carrinho NAO reserva estoque, entao expirar e so mudar o
    /// status — o efeito colateral util e liberar o slot do indice parcial de carrinho aberto
    /// por usuario, que e o que permite o cliente comecar um carrinho novo.
    /// Rastreado porque o worker vai justamente alterar o status de cada um.
    /// </summary>
    public async Task<IReadOnlyList<CarrinhoEntity>> ObterExpiradosAsync(
        DateTime agoraUtc,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        return await QueryTracked()
            .Where(c => c.Status == StatusCarrinho.Aberto && c.ExpiraEm <= agoraUtc)
            .OrderBy(c => c.ExpiraEm)
            .ThenBy(c => c.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Itens com variacao, produto, tamanho, cor e estoque: a tela do carrinho precisa disso
    /// tudo para desenhar a linha e dizer se ainda da para comprar.
    ///
    /// IgnoreQueryFilters de proposito. Variacao e Produto tem filtro de soft delete, e a
    /// navegacao obrigatoria do item viria nula se a peca fosse desativada com o carrinho
    /// cheio — a linha sumiria calada e o cliente veria o total mudar sozinho. Melhor carregar,
    /// mostrar "indisponivel" e deixar o checkout barrar com mensagem.
    /// </summary>
    private IQueryable<CarrinhoEntity> ConsultaCompleta() =>
        Query()
            .IgnoreQueryFilters()
            .Include(c => c.Cupom)
            .Include(c => c.Itens).ThenInclude(i => i.Variacao).ThenInclude(v => v.Produto)
                .ThenInclude(p => p.Midias).ThenInclude(m => m.Midia)
            .Include(c => c.Itens).ThenInclude(i => i.Variacao).ThenInclude(v => v.Tamanho)
            .Include(c => c.Itens).ThenInclude(i => i.Variacao).ThenInclude(v => v.Cor)
            .Include(c => c.Itens).ThenInclude(i => i.Variacao).ThenInclude(v => v.Estoque);
}
