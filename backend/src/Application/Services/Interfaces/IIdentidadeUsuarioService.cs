namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Traduz a identidade PUBLICA do token (claim sub, que carrega usuarios.Uuid) para a chave
/// interna int usada por todo o resto do sistema.
///
/// Por que existe: o Id inteiro nunca sai para o front — quem tem o Uuid nao consegue adivinhar
/// o vizinho incrementando um numero. O preco e que toda acao de cliente precisa converter uma
/// coisa na outra, e essa conversao nao pode ficar espalhada em cada controller, onde a chance de
/// alguem esquecer de checar se o usuario ainda esta ativo e alta.
/// </summary>
public interface IIdentidadeUsuarioService
{
    /// <summary>
    /// Devolve o Id interno do usuario do token. Lanca UnauthorizedAccessException (401 pelo
    /// middleware) quando o uuid esta ausente, nao existe ou pertence a conta desativada —
    /// token valido de conta desligada nao pode continuar comprando nem avaliando.
    /// </summary>
    Task<int> ObterIdPorUuidAsync(string? uuid, CancellationToken cancellationToken = default);
}
