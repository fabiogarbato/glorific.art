using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Catalogo;

/// <summary>
/// Criterio de ordenacao da vitrine. String livre na query vira ordenacao silenciosamente
/// ignorada; enum devolve 400 quando o valor nao existe.
/// </summary>
public enum OrdenacaoCatalogo
{
    Relevancia = 0,
    PrecoCrescente = 1,
    PrecoDecrescente = 2,
    Novidade = 3,
    MaisAvaliados = 4
}

/// <summary>
/// Filtro da vitrine.
///
/// NAO e um DTO de model binding: o controller recebe os parametros de query um a um e monta
/// este registro. Registro com { get; init; } ligado direto em [FromQuery] depende de detalhe
/// de binder, e um filtro que silenciosamente nao aplica e pior do que um erro.
/// </summary>
public sealed record CatalogoFiltro
{
    /// <summary>Slug da categoria. Inclui as FILHAS: /vestidos traz "Vestidos &gt; Midi".</summary>
    public string? Categoria { get; init; }

    public string? Colecao { get; init; }

    public GeneroProduto? Genero { get; init; }

    /// <summary>Codigos de tamanho (P, M, 38). Casa com qualquer um deles.</summary>
    public IReadOnlyList<string> Tamanhos { get; init; } = [];

    /// <summary>Slugs de cor (terracota, off-white).</summary>
    public IReadOnlyList<string> Cores { get; init; } = [];

    public int? PrecoMinCentavos { get; init; }

    public int? PrecoMaxCentavos { get; init; }

    /// <summary>Busca textual em nome, SKU base e descricao.</summary>
    public string? Busca { get; init; }

    /// <summary>
    /// Padrao TRUE: a vitrine mostra so o que da para comprar. False inclui a peca esgotada,
    /// que aparece com o badge — e o comportamento certo para link compartilhado e para SEO,
    /// onde sumir a pagina inteira e pior do que mostrar "esgotado".
    /// </summary>
    public bool SomenteDisponiveis { get; init; } = true;

    public bool? SomenteDestaques { get; init; }

    public OrdenacaoCatalogo Ordenacao { get; init; } = OrdenacaoCatalogo.Relevancia;
}

public sealed record CorVitrineDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string HexRgb { get; init; } = string.Empty;

    public string? UrlSwatch { get; init; }
}

public sealed record TamanhoVitrineDto
{
    public int Id { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public int Ordem { get; init; }

    public GradeTamanho Grade { get; init; }
}

/// <summary>Card da vitrine. Enxuto de proposito: sao ate 100 por pagina.</summary>
public sealed record ProdutoCardDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public GeneroProduto Genero { get; init; }

    public string? NomeCategoria { get; init; }

    public string? SlugCategoria { get; init; }

    /// <summary>Menor preco efetivo entre as variacoes: e o "a partir de" do card.</summary>
    public int PrecoAPartirDeCentavos { get; init; }

    public int? PrecoComparativoCentavos { get; init; }

    public string? UrlImagemCapa { get; init; }

    public string? AltImagemCapa { get; init; }

    public decimal? NotaMedia { get; init; }

    public int TotalAvaliacoes { get; init; }

    public bool Destaque { get; init; }

    /// <summary>
    /// Existe a peca, mas nenhum tamanho tem saldo livre. E o badge "esgotado" da vitrine.
    /// </summary>
    public bool Esgotado { get; init; }

    /// <summary>Swatches para o card — sem repeticao, na ordem de exibicao da cor.</summary>
    public IReadOnlyList<CorVitrineDto> Cores { get; init; } = [];

    /// <summary>Tamanhos COM saldo. Vazio quando esgotado.</summary>
    public IReadOnlyList<TamanhoVitrineDto> TamanhosDisponiveis { get; init; } = [];
}

public sealed record MidiaVitrineDto
{
    public int Id { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public int? Largura { get; init; }

    public int? Altura { get; init; }

    public int Ordem { get; init; }

    public bool EhCapa { get; init; }
}

/// <summary>
/// Galeria agrupada por cor: clicar no swatch "Terracota" troca as fotos. IdCor null e o grupo
/// neutro, exibido quando a cor selecionada nao tem foto propria.
/// </summary>
public sealed record GaleriaCorDto
{
    public int? IdCor { get; init; }

    public string? SlugCor { get; init; }

    public IReadOnlyList<MidiaVitrineDto> Midias { get; init; } = [];
}

/// <summary>Uma opcao compravel na pagina de produto: tamanho x cor com o saldo do momento.</summary>
public sealed record VariacaoVitrineDto
{
    public int Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public int IdTamanho { get; init; }

    public string CodigoTamanho { get; init; } = string.Empty;

    public int OrdemTamanho { get; init; }

    public int IdCor { get; init; }

    public string NomeCor { get; init; } = string.Empty;

    public string SlugCor { get; init; } = string.Empty;

    public string HexRgb { get; init; } = string.Empty;

    public int PrecoCentavos { get; init; }

    /// <summary>
    /// Quantidade LIVRE (fisico menos reservado). Exposta para o seletor desabilitar o tamanho
    /// sem saldo — e a informacao que evita o cliente descobrir no carrinho.
    /// </summary>
    public int QuantidadeDisponivel { get; init; }

    public bool Disponivel { get; init; }
}

/// <summary>Pagina de produto (PDP) completa.</summary>
public sealed record ProdutoDetalheDto : ResponseDto
{
    public int Id { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string SkuBase { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public GeneroProduto Genero { get; init; }

    public int IdCategoria { get; init; }

    public string? NomeCategoria { get; init; }

    public string? SlugCategoria { get; init; }

    public int PrecoBaseCentavos { get; init; }

    public int PrecoAPartirDeCentavos { get; init; }

    public int? PrecoComparativoCentavos { get; init; }

    public string? ComposicaoTecido { get; init; }

    public string? InstrucoesLavagem { get; init; }

    public ModelagemProduto? Modelagem { get; init; }

    public decimal? NotaMedia { get; init; }

    public int TotalAvaliacoes { get; init; }

    public bool Esgotado { get; init; }

    public string? MetaTitle { get; init; }

    public string? MetaDescription { get; init; }

    public IReadOnlyList<CorVitrineDto> Cores { get; init; } = [];

    public IReadOnlyList<TamanhoVitrineDto> Tamanhos { get; init; } = [];

    public IReadOnlyList<VariacaoVitrineDto> Variacoes { get; init; } = [];

    public IReadOnlyList<GaleriaCorDto> Galeria { get; init; } = [];

    /// <summary>Guia de medidas: item numero 1 de reducao de devolucao em moda.</summary>
    public TabelaMedidasResponseDto? TabelaMedidas { get; init; }

    public IReadOnlyList<ColecaoResponseDto> Colecoes { get; init; } = [];
}

public sealed record FacetaItemDto
{
    public int Id { get; init; }

    public string Rotulo { get; init; } = string.Empty;

    public string Valor { get; init; } = string.Empty;

    /// <summary>Cor traz o hex para o filtro pintar o swatch sem uma segunda chamada.</summary>
    public string? HexRgb { get; init; }

    public int Total { get; init; }
}

/// <summary>
/// Contagens para os filtros da vitrine. Sem elas o cliente clica em "GG" e recebe zero
/// resultado — o filtro precisa dizer de antemao o que existe.
/// </summary>
public sealed record FacetasCatalogoDto : ResponseDto
{
    public IReadOnlyList<FacetaItemDto> Categorias { get; init; } = [];

    public IReadOnlyList<FacetaItemDto> Colecoes { get; init; } = [];

    public IReadOnlyList<FacetaItemDto> Tamanhos { get; init; } = [];

    public IReadOnlyList<FacetaItemDto> Cores { get; init; } = [];

    public int PrecoMinCentavos { get; init; }

    public int PrecoMaxCentavos { get; init; }

    public int TotalProdutos { get; init; }
}
