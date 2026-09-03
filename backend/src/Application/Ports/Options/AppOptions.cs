using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "App". As duas URLs base do sistema.
///
/// Sao separadas porque sao coisas diferentes e erram de formas diferentes:
/// <see cref="PublicBaseUrl"/> e o endereco por onde o MUNDO alcanca a API (o gateway de
/// pagamento precisa entregar webhook nele — localhost aqui significa webhook que nunca chega),
/// e <see cref="LojaBaseUrl"/> e o endereco do site, usado em link de e-mail, tag de etiqueta e
/// redirect pos-pagamento.
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Base publica da API. Ex.: https://api.glorific.art
    /// Monta redirect_url e webhook_url da cobranca. Sem barra no fim.
    /// </summary>
    [Required(ErrorMessage = "App:PublicBaseUrl e obrigatoria.")]
    [Url(ErrorMessage = "App:PublicBaseUrl precisa ser uma URL absoluta.")]
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base do site. Ex.: https://glorific.art
    /// Monta os links do e-mail transacional e a url da tag da etiqueta no painel do ME.
    /// </summary>
    [Required(ErrorMessage = "App:LojaBaseUrl e obrigatoria.")]
    [Url(ErrorMessage = "App:LojaBaseUrl precisa ser uma URL absoluta.")]
    public string LojaBaseUrl { get; set; } = string.Empty;

    /// <summary>Nome exibido no remetente do e-mail e no titulo das paginas.</summary>
    [Required]
    public string NomeLoja { get; set; } = "Glorific";

    /// <summary>Destino dos alertas operacionais (envio falhou, saldo do ME baixo).</summary>
    [EmailAddress(ErrorMessage = "App:EmailAdministrativo precisa ser um e-mail valido.")]
    public string? EmailAdministrativo { get; set; }

    /// <summary>Concatena com a base publica da API garantindo uma unica barra.</summary>
    public string UrlApi(string caminhoRelativo) => Combinar(PublicBaseUrl, caminhoRelativo);

    /// <summary>Concatena com a base da loja garantindo uma unica barra.</summary>
    public string UrlLoja(string caminhoRelativo) => Combinar(LojaBaseUrl, caminhoRelativo);

    private static string Combinar(string baseUrl, string caminho) =>
        $"{(baseUrl ?? string.Empty).TrimEnd('/')}/{(caminho ?? string.Empty).TrimStart('/')}";
}
