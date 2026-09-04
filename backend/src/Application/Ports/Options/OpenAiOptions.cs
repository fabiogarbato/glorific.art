using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "OpenAI". Usada hoje só pelo gerador de descrição de produto (visão + texto).
///
/// A chave NUNCA aparece em log nem em resposta de API — só é lida aqui e injetada no header
/// Authorization do HttpClient tipado (ver Program.cs).
/// </summary>
public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    [Required(ErrorMessage = "OpenAI:ApiKey e obrigatoria.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [Url(ErrorMessage = "OpenAI:BaseUrl precisa ser uma URL valida.")]
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>
    /// Modelo com suporte a visão (lê a imagem do produto). "gpt-4o-mini" por padrão: rápido e
    /// barato o bastante para uma chamada por clique do admin.
    /// </summary>
    [Required]
    public string Modelo { get; set; } = "gpt-4o-mini";

    [Range(5, 120, ErrorMessage = "OpenAI:TimeoutSegundos deve estar entre 5 e 120.")]
    public int TimeoutSegundos { get; set; } = 45;
}
