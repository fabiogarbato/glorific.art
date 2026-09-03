namespace Glorific.Application.Ports;

/// <summary>
/// Porta de MATERIALIZACAO assincrona de IQueryable.
///
/// Por que ela existe: os repositorios do Domain expoem IQueryable de proposito (Query(),
/// QueryDisponiveis(), QueryDoUsuario()) para o caso de uso compor filtro e paginacao sem
/// carregar a tabela inteira. So que ToListAsync, CountAsync e FirstOrDefaultAsync sao
/// extensoes do Microsoft.EntityFrameworkCore — e o Application nao referencia EF.
///
/// Sem esta porta sobrariam duas saidas ruins: chamar .ToList()/.Count() sincronos (bloqueia
/// thread do pool em toda listagem) ou referenciar EF no Application (quebra a regra de camada).
/// Aqui o Application declara o que precisa e a Infrastructure, que ja conhece EF, implementa.
///
/// Regra dura mantida: nada aqui salva. Isto e so leitura.
/// </summary>
public interface IConsultaAssincrona
{
    /// <summary>Materializa a consulta inteira. Use sempre depois de Skip/Take.</summary>
    Task<IReadOnlyList<T>> ListarAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default);

    /// <summary>COUNT no banco, ignorando qualquer Skip/Take ja aplicado pelo chamador.</summary>
    Task<int> ContarAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default);

    Task<T?> PrimeiroOuPadraoAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default);

    /// <summary>EXISTS: nao traz linha nenhuma, so responde se ha alguma.</summary>
    Task<bool> AlgumAsync<T>(IQueryable<T> consulta, CancellationToken cancellationToken = default);
}
