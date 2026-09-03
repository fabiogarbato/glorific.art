namespace Glorific.Application.DTO.Identidade;

/// <summary>
/// Usuario como sai da API — no /auth/me, no perfil da conta e na listagem administrativa.
///
/// NUNCA carrega SenhaHash. <see cref="TemSenha"/> responde a unica pergunta que o front
/// realmente faz sobre a senha ("mostro o formulario de troca ou o convite para definir uma?")
/// sem que o hash saia do servidor.
/// </summary>
public sealed record UsuarioResponseDto : ResponseDto
{
    /// <summary>Chave interna. Usada apenas nas rotas administrativas (/admin/usuarios/{id}).</summary>
    public int Id { get; init; }

    /// <summary>Identificador publico. E o que vai na claim sub do access token.</summary>
    public string Uuid { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool EmailVerificado { get; init; }

    public string? NomeCompleto { get; init; }

    /// <summary>So digitos, como esta gravado.</summary>
    public string? Cpf { get; init; }

    public string? Telefone { get; init; }

    public string? FotoUrl { get; init; }

    public DateTime? DataNascimento { get; init; }

    public bool AceitaMarketing { get; init; }

    public bool Ativo { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? UltimoLoginEm { get; init; }

    /// <summary>Falso para quem entrou so por Google: o front esconde a troca de senha.</summary>
    public bool TemSenha { get; init; }

    public bool GoogleVinculado { get; init; }

    /// <summary>Papeis vindos de usuarios_roles. Mesma lista que vira claim role no JWT.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Derivado, so para a UI decidir se exibe o atalho do painel. NAO e autorizacao:
    /// quem decide acesso e a policy no servidor, nunca este booleano.
    /// </summary>
    // Qualificado ate a raiz de proposito: a propriedade Roles deste record esconde o tipo
    // Roles de Domain.Constants, e o nome curto compilaria apontando para o lugar errado.
    public bool Administrativo =>
        Roles.Any(papel => Glorific.Domain.Constants.Roles.Administrativos.Contains(papel));
}
