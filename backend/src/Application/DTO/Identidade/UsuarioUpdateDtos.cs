using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Identidade;

/// <summary>
/// O que o PROPRIO cliente pode alterar no seu perfil.
///
/// Repare no que NAO esta aqui: Email, Ativo, EmailVerificado e qualquer coisa ligada a papel.
/// Um campo a mais neste record e escalonamento de privilegio, porque o servico mapeia o que
/// existe. Troca de e-mail exige reverificacao e por isso e um fluxo proprio, nao um PUT.
/// </summary>
public sealed record PerfilUpdateDto : UpdateDto
{
    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "Nome completo invalido.")]
    public string NomeCompleto { get; init; } = string.Empty;

    [StringLength(20, ErrorMessage = "Telefone invalido.")]
    public string? Telefone { get; init; }

    [StringLength(14, ErrorMessage = "CPF invalido.")]
    public string? Cpf { get; init; }

    public DateTime? DataNascimento { get; init; }

    public bool AceitaMarketing { get; init; }
}

/// <summary>
/// Edicao administrativa de um usuario (policy SomenteAdmin).
///
/// Papel continua fora: conceder e revogar tem endpoint proprio porque sao acoes auditaveis,
/// com regra de "nao pode mexer em si mesmo". Enfiar uma lista de roles num PUT generico
/// esconderia a decisao mais perigosa do sistema dentro de um formulario de cadastro.
/// </summary>
public sealed record UsuarioAdminUpdateDto : UpdateDto
{
    [StringLength(180, MinimumLength = 2, ErrorMessage = "Nome completo invalido.")]
    public string? NomeCompleto { get; init; }

    [StringLength(20, ErrorMessage = "Telefone invalido.")]
    public string? Telefone { get; init; }

    [StringLength(14, ErrorMessage = "CPF invalido.")]
    public string? Cpf { get; init; }

    public bool AceitaMarketing { get; init; }
}
