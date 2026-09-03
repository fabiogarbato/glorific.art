using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class MidiaRepository : BaseRepository<Midia>, IMidiaRepository
{
    public MidiaRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public Task<Midia?> ObterPorPublicIdAsync(string publicId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(m => m.PublicId == publicId, cancellationToken);

    /// <summary>
    /// Midias sem NENHUM vinculo, mais velhas que o corte.
    ///
    /// Upload interrompido no meio do formulario deixa arquivo pago no storage para sempre se
    /// ninguem varrer. O corte por data existe porque a midia recem-enviada e legitimamente
    /// orfa entre o upload e o "salvar" do admin — apagar sem essa folga mataria o upload em
    /// andamento.
    ///
    /// Os cinco NOT EXISTS cobrem todos os donos possiveis de uma midia. Faltar um deles
    /// significa apagar do storage um arquivo que a vitrine ainda referencia.
    /// </summary>
    public async Task<IReadOnlyList<Midia>> ObterOrfasAsync(
        DateTime anterioresA,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return [];

        return await Query()
            .Where(m => m.DataCriacao < anterioresA
                        && !Contexto.MidiasProdutos.Any(mp => mp.IdMidia == m.Id)
                        && !Contexto.AvaliacoesMidias.Any(am => am.IdMidia == m.Id)
                        && !Contexto.Categorias.Any(c => c.IdMidiaCapa == m.Id)
                        && !Contexto.Colecoes.Any(c => c.IdMidiaCapa == m.Id || c.IdMidiaBanner == m.Id)
                        && !Contexto.Cores.Any(c => c.IdMidiaSwatch == m.Id))
            .OrderBy(m => m.Id)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Galeria do produto na ordem de exibicao: capa primeiro, depois o campo Ordem.
    /// A cor vem junto porque a galeria filtra por swatch na pagina do produto.
    /// </summary>
    public async Task<IReadOnlyList<MidiaProduto>> ObterGaleriaAsync(
        int idProduto,
        CancellationToken cancellationToken = default) =>
        await Contexto.MidiasProdutos
            .AsNoTracking()
            .Where(mp => mp.IdProduto == idProduto)
            .Include(mp => mp.Midia)
            .Include(mp => mp.Cor)
            .OrderByDescending(mp => mp.EhCapa)
            .ThenBy(mp => mp.Ordem)
            .ThenBy(mp => mp.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Reordena a galeria em bloco. A capa e a primeira da lista, por Ordem EXPLICITA: deduzir
    /// capa pelo menor Id troca a foto principal a cada reupload.
    ///
    /// Escrita rastreada de proposito, e nao ExecuteUpdate — sao poucas linhas, a nova ordem
    /// depende da posicao no array (nao da para expressar em um UPDATE unico sem CASE) e assim
    /// a reordenacao entra na MESMA unidade de trabalho do resto da edicao do produto. Nao
    /// salva: quem salva e o caso de uso.
    ///
    /// Ids que nao pertencem ao produto sao ignorados — o payload vem do navegador.
    /// </summary>
    public async Task ReordenarGaleriaAsync(
        int idProduto,
        IReadOnlyList<int> idsMidiaProdutoNaOrdem,
        CancellationToken cancellationToken = default)
    {
        if (idsMidiaProdutoNaOrdem is null || idsMidiaProdutoNaOrdem.Count == 0)
            return;

        var ids = idsMidiaProdutoNaOrdem.Distinct().ToArray();

        var itens = await Contexto.MidiasProdutos
            .Where(mp => mp.IdProduto == idProduto && ids.Contains(mp.Id))
            .ToListAsync(cancellationToken);

        for (var posicao = 0; posicao < idsMidiaProdutoNaOrdem.Count; posicao++)
        {
            var item = itens.FirstOrDefault(mp => mp.Id == idsMidiaProdutoNaOrdem[posicao]);

            if (item is null)
                continue;

            item.Ordem = posicao;
            item.EhCapa = posicao == 0;
        }
    }
}
