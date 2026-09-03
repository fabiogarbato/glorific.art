using System.ComponentModel.DataAnnotations;
using Glorific.Application.Common;

namespace Glorific.Application.DTO.Identidade;

/// <summary>
/// Cadastro publico por e-mail e senha. O papel NAO entra aqui: quem se cadastra pela loja
/// nasce sempre "cliente". Aceitar papel vindo do corpo e auto-escalonamento de graca.
/// </summary>
public sealed record RegistroRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    [StringLength(255, ErrorMessage = "E-mail longo demais.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Minimo de 8 e maximo em bytes do BCrypt: acima de 72 bytes o algoritmo IGNORA o resto,
    /// entao aceitar mais seria prometer uma forca que o hash nao tem.
    /// </summary>
    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(Senhas.MaximoBytes, MinimumLength = 8,
        ErrorMessage = "A senha precisa ter de 8 a 72 caracteres.")]
    public string Senha { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "Nome completo invalido.")]
    public string NomeCompleto { get; init; } = string.Empty;

    [StringLength(20, ErrorMessage = "Telefone invalido.")]
    public string? Telefone { get; init; }

    [StringLength(14, ErrorMessage = "CPF invalido.")]
    public string? Cpf { get; init; }

    public bool AceitaMarketing { get; init; }
}

/// <summary>Login por e-mail e senha. Mantido para admin, gerente e operador.</summary>
public sealed record LoginRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(Senhas.MaximoBytes)]
    public string Senha { get; init; } = string.Empty;
}

/// <summary>
/// Corpo de /auth/google e /auth/link-google. O front obtem o id_token via Google Identity
/// Services e o back valida a assinatura — o cliente nunca manda o proprio e-mail para ser
/// aceito de graca.
/// </summary>
public sealed record GoogleLoginRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o idToken do Google.")]
    [StringLength(4096, ErrorMessage = "idToken invalido.")]
    public string IdToken { get; init; } = string.Empty;
}

/// <summary>Troca de senha com o usuario ja autenticado.</summary>
public sealed record TrocarSenhaRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    [StringLength(Senhas.MaximoBytes)]
    public string SenhaAtual { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(Senhas.MaximoBytes, MinimumLength = 8,
        ErrorMessage = "A nova senha precisa ter de 8 a 72 caracteres.")]
    public string NovaSenha { get; init; } = string.Empty;
}

/// <summary>Pedido de link de redefinicao. A resposta e sempre 204, exista o e-mail ou nao.</summary>
public sealed record EsqueciSenhaRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Redefinicao com o token recebido por e-mail.</summary>
public sealed record RedefinirSenhaRequestDto : CreateDto
{
    [Required(ErrorMessage = "Token de redefinicao ausente.")]
    [StringLength(512, ErrorMessage = "Token de redefinicao invalido.")]
    public string Token { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(Senhas.MaximoBytes, MinimumLength = 8,
        ErrorMessage = "A nova senha precisa ter de 8 a 72 caracteres.")]
    public string NovaSenha { get; init; } = string.Empty;
}
