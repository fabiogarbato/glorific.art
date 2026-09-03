using Glorific.Domain.Entities.Promocoes;

namespace Glorific.Domain.Interfaces.Repositories;

public interface ICupomRepository : IBaseRepository<Cupom>
{
    /// <summary>O codigo e normalizado em maiusculas antes da busca.</summary>
    Task<Cupom?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    Task<bool> CodigoEmUsoAsync(string codigo, int? idIgnorar = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// UPDATE cupons SET usos_atuais = usos_atuais + 1
    /// WHERE id = id AND ativo AND (uso_maximo_total IS NULL OR usos_atuais menor que uso_maximo_total).
    /// False significa cupom esgotado. Ler e depois incrementar deixaria dois checkouts
    /// simultaneos consumirem o ultimo uso do cupom dos primeiros cem.
    /// </summary>
    Task<bool> TentarConsumirUsoAsync(int idCupom, CancellationToken cancellationToken = default);

    /// <summary>Devolve o uso quando o checkout falha depois de ter consumido.</summary>
    Task DevolverUsoAsync(int idCupom, CancellationToken cancellationToken = default);

    /// <summary>Sustenta a regra de uso maximo por usuario sem carregar o ledger inteiro.</summary>
    Task<int> ContarUsosDoUsuarioAsync(int idCupom, int idUsuario, CancellationToken cancellationToken = default);

    Task RegistrarUsoAsync(CupomUso uso, CancellationToken cancellationToken = default);
}
