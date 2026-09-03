using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IMidiaRepository : IBaseRepository<Midia>
{
    Task<Midia?> ObterPorPublicIdAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Midias sem nenhum vinculo. Upload interrompido no meio do formulario deixa arquivo pago
    /// no storage para sempre se ninguem varrer.
    /// </summary>
    Task<IReadOnlyList<Midia>> ObterOrfasAsync(DateTime anterioresA, int limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MidiaProduto>> ObterGaleriaAsync(int idProduto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reordena a galeria em bloco. A capa e definida por Ordem explicita porque deduzir por
    /// menor Id troca a foto principal a cada reupload.
    /// </summary>
    Task ReordenarGaleriaAsync(int idProduto, IReadOnlyList<int> idsMidiaProdutoNaOrdem, CancellationToken cancellationToken = default);
}
