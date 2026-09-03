using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class TabelaMedidasRepository : BaseRepository<TabelaMedidas>, ITabelaMedidasRepository
{
    public TabelaMedidasRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Com as linhas na ordem do tamanho: a tabela e exibida inteira ou nao serve para nada.
    /// A ordem sai do campo Ordem da linha, com o Ordem do tamanho como desempate — ordenar por
    /// codigo colocaria GG antes de P.
    /// </summary>
    public Task<TabelaMedidas?> ObterComLinhasAsync(int id, CancellationToken cancellationToken = default) =>
        Query()
            .Include(t => t.Linhas.OrderBy(l => l.Ordem))
                .ThenInclude(l => l.Tamanho)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>
    /// Catalogo publico do guia de medidas: so o que esta ATIVO, ja com as linhas na ordem da
    /// grade e com o Tamanho carregado (sem ele a coluna de codigo sai vazia na loja).
    ///
    /// Uma unica consulta com Include, e nao uma por tabela: sao poucas tabelas de medidas e a
    /// pagina exibe todas de uma vez.
    /// </summary>
    public async Task<IReadOnlyList<TabelaMedidas>> ListarAtivasComLinhasAsync(
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(t => t.Ativo)
            .Include(t => t.Linhas.OrderBy(l => l.Ordem).ThenBy(l => l.Id))
                .ThenInclude(l => l.Tamanho)
            .OrderBy(t => t.Nome)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// A mesma leitura para UMA tabela. Inativa devolve null de proposito: para quem nao esta no
    /// painel, tabela desativada e tabela inexistente.
    /// </summary>
    public Task<TabelaMedidas?> ObterAtivaComLinhasAsync(int id, CancellationToken cancellationToken = default) =>
        Query()
            .Where(t => t.Ativo)
            .Include(t => t.Linhas.OrderBy(l => l.Ordem).ThenBy(l => l.Id))
                .ThenInclude(l => l.Tamanho)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>
    /// Barra a exclusao antes de o Restrict do banco virar erro cru na tela do admin.
    /// IgnoreQueryFilters: produto desativado continua apontando para a tabela e a FK continua
    /// impedindo o delete.
    /// </summary>
    public Task<bool> PossuiProdutosVinculadosAsync(int id, CancellationToken cancellationToken = default) =>
        Contexto.Produtos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.IdTabelaMedidas == id, cancellationToken);
}
