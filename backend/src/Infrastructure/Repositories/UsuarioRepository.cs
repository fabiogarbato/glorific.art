using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class UsuarioRepository : BaseRepository<Usuario>, IUsuarioRepository
{
    /// <summary>Status em que o dinheiro ja entrou — o que conta como "compra feita".</summary>
    private static readonly StatusPedido[] StatusPagos =
    [
        StatusPedido.Pago,
        StatusPedido.EmSeparacao,
        StatusPedido.Enviado,
        StatusPedido.Entregue
    ];

    public UsuarioRepository(GlorificContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Casa por e-mail ja normalizado em minusculas. A normalizacao acontece tambem aqui porque
    /// o indice unico e sobre o valor gravado: deixar so para o chamador significa que um unico
    /// ponto esquecido cria a segunda conta do mesmo cliente.
    /// </summary>
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        return Query().FirstOrDefaultAsync(u => u.Email == normalizado, cancellationToken);
    }

    public Task<Usuario?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(u => u.Uuid == uuid, cancellationToken);

    /// <summary>
    /// Casa pelo par (provedor, subject). O subject do Google e imutavel; casar por e-mail
    /// deixaria a conta orfa no dia em que o cliente troca o endereco na conta Google.
    /// </summary>
    public async Task<Usuario?> ObterPorLoginExternoAsync(
        string provedor,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var idUsuario = await Contexto.LoginsExternos
            .AsNoTracking()
            .Where(l => l.Provedor == provedor && l.SubjectId == subjectId)
            .Select(l => (int?)l.IdUsuario)
            .FirstOrDefaultAsync(cancellationToken);

        if (idUsuario is null)
            return null;

        return await Query()
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == idUsuario, cancellationToken);
    }

    /// <summary>Com os papeis carregados: o token so pode ser emitido depois disso.</summary>
    public Task<Usuario?> ObterComRolesAsync(int id, CancellationToken cancellationToken = default) =>
        Query()
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> EmailEmUsoAsync(
        string email,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        return Query().AnyAsync(
            u => u.Email == normalizado && (idIgnorar == null || u.Id != idIgnorar),
            cancellationToken);
    }

    /// <summary>
    /// O indice de CPF e parcial (WHERE cpf IS NOT NULL): conta sem CPF nao disputa unicidade
    /// com outra conta sem CPF. A checagem aqui segue a mesma regra e ignora valor vazio.
    /// </summary>
    public Task<bool> CpfEmUsoAsync(
        string cpf,
        int? idIgnorar = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return Task.FromResult(false);

        return Query().AnyAsync(
            u => u.Cpf == cpf && (idIgnorar == null || u.Id != idIgnorar),
            cancellationToken);
    }

    /// <summary>
    /// Responde a regra de cupom PrimeiraCompraApenas sem carregar os pedidos. EXISTS no banco:
    /// o cliente antigo tem centenas de linhas e a resposta e um booleano.
    /// </summary>
    public Task<bool> PossuiPedidoPagoAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Contexto.Pedidos
            .AsNoTracking()
            .AnyAsync(
                p => p.IdUsuario == idUsuario && StatusPagos.Contains(p.Status),
                cancellationToken);

    public async Task AdicionarLoginExternoAsync(
        LoginExterno login,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);
        await Contexto.LoginsExternos.AddAsync(login, cancellationToken);
    }

    /// <summary>Rastreado: quem acha o vinculo grava UltimoUsoEm na sequencia.</summary>
    public Task<LoginExterno?> ObterLoginExternoAsync(
        string provedor,
        string subjectId,
        CancellationToken cancellationToken = default) =>
        Contexto.LoginsExternos
            .FirstOrDefaultAsync(
                l => l.Provedor == provedor && l.SubjectId == subjectId,
                cancellationToken);
}
