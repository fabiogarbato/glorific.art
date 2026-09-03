using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class PedidoRepository : BaseRepository<Pedido>, IPedidoRepository
{
    /// <summary>Prefixo do numero humano do pedido: GA-2026-000137.</summary>
    private const string PrefixoNumero = "GA";

    private const int DigitosSequencial = 6;

    public PedidoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Pedido?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.Numero == numero, cancellationToken);

    public Task<Pedido?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.Uuid == uuid, cancellationToken);

    /// <summary>
    /// Pedido inteiro: itens, pagamento, envio e historico.
    ///
    /// IgnoreQueryFilters e obrigatorio aqui. PedidoItem aponta para Produto, que tem filtro de
    /// soft delete (Ativo). Pedido de dois anos atras tem item de produto ja desativado; com o
    /// filtro ligado, a navegacao obrigatoria vem nula e o recibo abre sem as linhas. O snapshot
    /// de nome, sku, tamanho e preco vive no proprio item justamente para o recibo nunca mudar,
    /// mas a navegacao ainda precisa carregar para a tela de detalhe.
    /// </summary>
    public Task<Pedido?> ObterCompletoAsync(int id, CancellationToken cancellationToken = default) =>
        ConsultaCompleta().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>
    /// Sempre filtrando por usuario na propria consulta.
    ///
    /// Buscar so pelo uuid e comparar o dono em memoria vaza existencia do pedido alheio: o
    /// atacante distingue 403 de 404 e enumera. Com o filtro dentro do WHERE, pedido de outra
    /// pessoa simplesmente nao existe.
    /// </summary>
    public Task<Pedido?> ObterDoUsuarioAsync(
        int idUsuario,
        string uuid,
        CancellationToken cancellationToken = default) =>
        ConsultaCompleta().FirstOrDefaultAsync(
            p => p.IdUsuario == idUsuario && p.Uuid == uuid,
            cancellationToken);

    /// <summary>
    /// Base da listagem "meus pedidos". Devolve IQueryable para o caso de uso aplicar Skip/Take
    /// server-side; nada e materializado aqui. IgnoreQueryFilters pelo mesmo motivo do detalhe:
    /// historico precisa enxergar produto desativado.
    /// </summary>
    public IQueryable<Pedido> QueryDoUsuario(int idUsuario) =>
        Query()
            .IgnoreQueryFilters()
            .Where(p => p.IdUsuario == idUsuario)
            .Include(p => p.Itens)
            .Include(p => p.Pagamento)
            .Include(p => p.Envio)
            .OrderByDescending(p => p.DataCriacao)
            .ThenByDescending(p => p.Id);

    /// <summary>
    /// Proximo sequencial humano do ano, no formato GA-2026-000137.
    ///
    /// O numero e ordenavel como texto porque o sequencial tem largura fixa com zeros a
    /// esquerda — sem isso, "GA-2026-1000" viria antes de "GA-2026-999". A corrida entre dois
    /// checkouts simultaneos e resolvida pelo indice unico ux_pedidos_numero: quem perder
    /// estoura violacao e o caso de uso repete a geracao.
    /// </summary>
    public async Task<string> GerarProximoNumeroAsync(int ano, CancellationToken cancellationToken = default)
    {
        var prefixo = $"{PrefixoNumero}-{ano}-";

        var ultimo = await Query()
            .Where(p => p.Numero.StartsWith(prefixo))
            .OrderByDescending(p => p.Numero)
            .Select(p => p.Numero)
            .FirstOrDefaultAsync(cancellationToken);

        var proximo = 1;

        if (!string.IsNullOrEmpty(ultimo)
            && ultimo.Length > prefixo.Length
            && int.TryParse(ultimo[prefixo.Length..], out var sequencialAtual))
        {
            proximo = sequencialAtual + 1;
        }

        return prefixo + proximo.ToString(new string('0', DigitosSequencial));
    }

    /// <summary>
    /// Fila do worker de expiracao: aguardando pagamento com prazo do gateway ja vencido.
    /// Vem com os itens porque cancelar significa liberar a reserva de estoque de cada linha —
    /// que e o motivo de o worker existir.
    /// </summary>
    public async Task<IReadOnlyList<Pedido>> ObterAguardandoPagamentoVencidosAsync(
        DateTime agoraUtc,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        return await Query()
            .IgnoreQueryFilters()
            .Where(p => p.Status == StatusPedido.AguardandoPagamento
                        && p.Pagamento != null
                        && p.Pagamento.ExpiraEm != null
                        && p.Pagamento.ExpiraEm <= agoraUtc)
            .Include(p => p.Itens)
            .Include(p => p.Pagamento)
            .OrderBy(p => p.DataCriacao)
            .ThenBy(p => p.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Historico e append-only: so existe registrar, nunca alterar ou remover.</summary>
    public async Task RegistrarHistoricoAsync(
        PedidoHistorico historico,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(historico);
        await Contexto.PedidosHistorico.AddAsync(historico, cancellationToken);
    }

    /// <summary>
    /// Grafo completo do pedido, sempre com IgnoreQueryFilters. Ver ObterCompletoAsync.
    /// </summary>
    private IQueryable<Pedido> ConsultaCompleta() =>
        Query()
            .IgnoreQueryFilters()
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .Include(p => p.Itens).ThenInclude(i => i.Variacao)
            .Include(p => p.Pagamento)
            .Include(p => p.Envio)
            .Include(p => p.Historico)
            .Include(p => p.Cupom);
}
