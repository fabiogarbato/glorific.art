using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class CupomRepository : BaseRepository<Cupom>, ICupomRepository
{
    public CupomRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>O codigo e sempre normalizado em maiusculas antes de bater no indice unico.</summary>
    public Task<Cupom?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var normalizado = Normalizar(codigo);

        return Query().FirstOrDefaultAsync(c => c.Codigo == normalizado, cancellationToken);
    }

    public Task<bool> CodigoEmUsoAsync(
        string codigo,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = Normalizar(codigo);

        return Query().AnyAsync(
            c => c.Codigo == normalizado && (idIgnorar == null || c.Id != idIgnorar),
            cancellationToken);
    }

    /// <summary>
    /// UPDATE cupons SET usos_atuais = usos_atuais + 1
    /// WHERE id = @id AND ativo AND (uso_maximo_total IS NULL OR usos_atuais &lt; uso_maximo_total).
    ///
    /// False significa cupom esgotado, nao falha tecnica. Ler o cupom e depois incrementar em
    /// duas idas deixaria dois checkouts simultaneos consumirem o mesmo ultimo uso do
    /// "primeiros cem" — e o lojista honraria cento e um.
    ///
    /// ExecuteUpdateAsync nao mexe no identity map: a linha e desanexada logo depois, e quem
    /// precisar de UsosAtuais atualizado tem que reconsultar.
    /// </summary>
    public async Task<bool> TentarConsumirUsoAsync(int idCupom, CancellationToken cancellationToken = default)
    {
        var linhas = await Contexto.Cupons
            .Where(c => c.Id == idCupom
                        && c.Ativo
                        && (c.UsoMaximoTotal == null || c.UsosAtuais < c.UsoMaximoTotal))
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.UsosAtuais, c => c.UsosAtuais + 1),
                cancellationToken);

        DesanexarCupom(idCupom);
        return linhas > 0;
    }

    /// <summary>
    /// Devolve o uso quando o checkout falha depois de ter consumido.
    /// O WHERE usos_atuais > 0 impede contador negativo em compensacao executada duas vezes.
    /// </summary>
    public async Task DevolverUsoAsync(int idCupom, CancellationToken cancellationToken = default)
    {
        await Contexto.Cupons
            .Where(c => c.Id == idCupom && c.UsosAtuais > 0)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.UsosAtuais, c => c.UsosAtuais - 1),
                cancellationToken);

        DesanexarCupom(idCupom);
    }

    /// <summary>COUNT no banco: a regra de uso maximo por usuario nao carrega o ledger inteiro.</summary>
    public Task<int> ContarUsosDoUsuarioAsync(
        int idCupom,
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        Contexto.CuponsUsos
            .AsNoTracking()
            .CountAsync(u => u.IdCupom == idCupom && u.IdUsuario == idUsuario, cancellationToken);

    /// <summary>Registra a intencao; quem salva e o caso de uso, dentro da transacao do checkout.</summary>
    public async Task RegistrarUsoAsync(CupomUso uso, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uso);
        await Contexto.CuponsUsos.AddAsync(uso, cancellationToken);
    }

    private static string Normalizar(string codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    private void DesanexarCupom(int idCupom) =>
        DesanexarRastreados<Cupom>(c => c.Id == idCupom);
}
