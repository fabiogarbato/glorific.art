using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Interfaces.Repositories;

namespace Glorific.Application.Services;

/// <inheritdoc cref="IIdentidadeUsuarioService" />
public sealed class IdentidadeUsuarioService : IIdentidadeUsuarioService
{
    private readonly IUsuarioRepository _usuarios;

    public IdentidadeUsuarioService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
    }

    /// <inheritdoc />
    public async Task<int> ObterIdPorUuidAsync(string? uuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            throw new UnauthorizedAccessException("Token sem identificacao de usuario.");

        var usuario = await _usuarios.ObterPorUuidAsync(uuid.Trim(), cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuario do token nao existe mais.");

        // Conta desativada com token ainda dentro da validade continua passando pela assinatura.
        // A checagem de estado tem de ser aqui, no servidor, e nao no ato de emitir o token.
        if (!usuario.Ativo)
            throw new UnauthorizedAccessException("Conta desativada.");

        return usuario.Id;
    }
}
