namespace Glorific.Infrastructure.Storage;

/// <summary>
/// Secao "Storage:Local". Configuracao do armazenamento de imagem em disco.
///
/// Existe como Options e nao como constante porque os dois valores que mudam entre ambientes
/// sao justamente os que quebram silenciosamente: a pasta fisica (host x container) e a URL
/// publica (localhost x dominio). URL publica errada faz a vitrine renderizar com a foto
/// quebrada, sem nenhum erro no log.
/// </summary>
public sealed class ArmazenamentoLocalOptions
{
    public const string SectionName = "Storage:Local";

    /// <summary>
    /// Raiz fisica servida como estatico. Relativa ao diretorio de execucao quando nao e um
    /// caminho absoluto. No container, apontar para um VOLUME: sem volume, todo deploy apaga o
    /// acervo de fotos junto com a imagem antiga.
    /// </summary>
    public string PastaRaiz { get; set; } = "wwwroot";

    /// <summary>Subpasta e segmento de URL das midias. Sem barra no inicio nem no fim.</summary>
    public string Pasta { get; set; } = "media";

    /// <summary>
    /// Base publica da URL gerada. Vazio devolve URL RELATIVA (/media/...), que e o certo quando
    /// a API e o site saem pelo mesmo dominio atras do proxy.
    /// </summary>
    public string? BaseUrlPublica { get; set; }

    /// <summary>8 MB. Foto de catalogo tratada nao passa disso; o que passa e arquivo cru de camera.</summary>
    public long TamanhoMaximoBytes { get; set; } = 8L * 1024 * 1024;

    /// <summary>
    /// Formatos aceitos. SVG fica de fora de proposito: e vetor com script, e servir SVG enviado
    /// pelo painel a partir do proprio dominio e um XSS armazenado.
    /// </summary>
    public IList<string> ContentTypesPermitidos { get; set; } =
    [
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/webp",
        "image/avif"
    ];
}
