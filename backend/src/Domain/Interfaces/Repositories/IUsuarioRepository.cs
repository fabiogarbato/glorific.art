using Glorific.Domain.Entities.Identidade;

namespace Glorific.Domain.Interfaces.Repositories;

public interface IUsuarioRepository : IBaseRepository<Usuario>
{
    /// <summary>Casa por e-mail ja normalizado em minusculas.</summary>
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Usuario?> ObterPorUuidAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Casa pelo par (provedor, subject). O subject do Google e imutavel; casar por e-mail
    /// deixaria a conta orfa no dia em que o cliente troca o endereco na conta Google.
    /// </summary>
    Task<Usuario?> ObterPorLoginExternoAsync(string provedor, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>Com os papeis carregados: o token so pode ser emitido depois disso.</summary>
    Task<Usuario?> ObterComRolesAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> EmailEmUsoAsync(string email, int? idIgnorar = null, CancellationToken cancellationToken = default);

    Task<bool> CpfEmUsoAsync(string cpf, int? idIgnorar = null, CancellationToken cancellationToken = default);

    /// <summary>Responde a regra de cupom PrimeiraCompraApenas sem carregar os pedidos.</summary>
    Task<bool> PossuiPedidoPagoAsync(int idUsuario, CancellationToken cancellationToken = default);

    Task AdicionarLoginExternoAsync(LoginExterno login, CancellationToken cancellationToken = default);

    Task<LoginExterno?> ObterLoginExternoAsync(string provedor, string subjectId, CancellationToken cancellationToken = default);
}
