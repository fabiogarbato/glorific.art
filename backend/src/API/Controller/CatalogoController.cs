using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Glorific.Api.Controller;

/// <summary>
/// Vitrine publica: catalogo, pagina de produto e os dados que alimentam os filtros.
///
/// [AllowAnonymous] na CLASSE e obrigatorio e explicito — a FallbackPolicy do Program exige
/// usuario autenticado em qualquer endpoint sem atributo. Aqui o anonimo e a regra: a loja
/// precisa abrir para quem nunca entrou, inclusive para o robo de indexacao.
///
/// A rota nao segue o padrao /api/v1/catalogo em todas as actions de proposito: o blueprint
/// define /produtos/{slug}, /categorias e /colecoes como enderecos de primeira classe, porque
/// sao eles que aparecem na URL do site e no sitemap.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1")]
[Produces("application/json")]
public sealed class CatalogoController : ControllerBase
{
    private readonly IProdutoService _produtos;
    private readonly ICategoriaService _categorias;
    private readonly IColecaoService _colecoes;
    private readonly ITamanhoService _tamanhos;
    private readonly ICorService _cores;

    public CatalogoController(
        IProdutoService produtos,
        ICategoriaService categorias,
        IColecaoService colecoes,
        ITamanhoService tamanhos,
        ICorService cores)
    {
        _produtos = produtos;
        _categorias = categorias;
        _colecoes = colecoes;
        _tamanhos = tamanhos;
        _cores = cores;
    }

    /// <summary>
    /// Vitrine paginada com todos os filtros de moda.
    ///
    /// emEstoque = true por padrao: a loja mostra o que da para comprar. Passar false inclui a
    /// peca esgotada, com o badge — util para link compartilhado e para SEO, onde sumir a
    /// pagina inteira e pior do que exibir "esgotado".
    /// </summary>
    [HttpGet("catalogo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProdutoCardDto>>> Listar(
        [FromQuery] string? categoria,
        [FromQuery] string? colecao,
        [FromQuery] GeneroProduto? genero,
        [FromQuery] string? tamanhos,
        [FromQuery] string? cores,
        [FromQuery] int? precoMin,
        [FromQuery] int? precoMax,
        [FromQuery] string? q,
        [FromQuery] bool? emEstoque,
        [FromQuery] bool? destaque,
        [FromQuery] OrdenacaoCatalogo? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var filtro = MontarFiltro(
            categoria, colecao, genero, tamanhos, cores, precoMin, precoMax, q, emEstoque, destaque, sort);

        // PageRequest normaliza no construtor: page=0 vira 1 e pageSize=999999 vira o teto.
        var resultado = await _produtos.ListarVitrineAsync(
            filtro, new PageRequest(page, pageSize), cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Contagens por categoria, colecao, tamanho, cor e faixa de preco.
    /// Sem elas o cliente clica em "GG" e recebe zero resultado.
    /// </summary>
    [HttpGet("catalogo/facetas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FacetasCatalogoDto>> Facetas(
        [FromQuery] string? categoria,
        [FromQuery] string? colecao,
        [FromQuery] GeneroProduto? genero,
        [FromQuery] string? tamanhos,
        [FromQuery] string? cores,
        [FromQuery] int? precoMin,
        [FromQuery] int? precoMax,
        [FromQuery] string? q,
        [FromQuery] bool? emEstoque,
        CancellationToken cancellationToken)
    {
        var filtro = MontarFiltro(
            categoria, colecao, genero, tamanhos, cores, precoMin, precoMax, q, emEstoque, null, null);

        return Ok(await _produtos.ObterFacetasAsync(filtro, cancellationToken));
    }

    /// <summary>Pagina de produto: variacoes com saldo, galeria por cor e tabela de medidas.</summary>
    [HttpGet("produtos/{slug}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDetalheDto>> ObterPorSlug(string slug, CancellationToken cancellationToken) =>
        Ok(await _produtos.ObterDetalhePorSlugAsync(slug, cancellationToken));

    [HttpGet("produtos/{slug}/relacionados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProdutoCardDto>>> Relacionados(
        string slug,
        [FromQuery] int? limite,
        CancellationToken cancellationToken) =>
        Ok(await _produtos.ObterRelacionadosAsync(slug, limite ?? 8, cancellationToken));

    /// <summary>Arvore de UM nivel (pai + filhas habilitadas): o menu do site inteiro.</summary>
    [HttpGet("categorias")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoriaResponseDto>>> Categorias(
        CancellationToken cancellationToken) =>
        Ok(await _categorias.ObterArvoreAsync(somenteHabilitadas: true, cancellationToken));

    [HttpGet("categorias/{slug}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaResponseDto>> Categoria(string slug, CancellationToken cancellationToken) =>
        Ok(await _categorias.ObterPorSlugAsync(slug, cancellationToken));

    /// <summary>So as VIGENTES: e o que faz o drop agendado entrar no ar sozinho.</summary>
    [HttpGet("colecoes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ColecaoResponseDto>>> Colecoes(CancellationToken cancellationToken) =>
        Ok(await _colecoes.ObterVigentesAsync(cancellationToken));

    [HttpGet("colecoes/{slug}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ColecaoResponseDto>> Colecao(string slug, CancellationToken cancellationToken) =>
        Ok(await _colecoes.ObterPorSlugAsync(slug, cancellationToken));

    /// <summary>Tamanhos ativos na ordem da grade — nunca alfabetica.</summary>
    [HttpGet("tamanhos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TamanhoResponseDto>>> Tamanhos(
        [FromQuery] GradeTamanho? grade,
        CancellationToken cancellationToken) =>
        Ok(await _tamanhos.ObterAtivosOrdenadosAsync(grade, cancellationToken));

    [HttpGet("cores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CorResponseDto>>> Cores(CancellationToken cancellationToken) =>
        Ok(await _cores.ObterAtivasOrdenadasAsync(cancellationToken));

    /// <summary>
    /// Monta o filtro a partir da query string.
    ///
    /// Os parametros chegam um a um, e nao como um record ligado com [FromQuery], porque
    /// registro com { get; init; } depende de detalhe do binder — e um filtro que silenciosamente
    /// nao e aplicado e pior do que um erro: a loja mostra o catalogo inteiro e ninguem percebe.
    /// </summary>
    private static CatalogoFiltro MontarFiltro(
        string? categoria,
        string? colecao,
        GeneroProduto? genero,
        string? tamanhos,
        string? cores,
        int? precoMin,
        int? precoMax,
        string? busca,
        bool? emEstoque,
        bool? destaque,
        OrdenacaoCatalogo? ordenacao) =>
        new()
        {
            Categoria = categoria,
            Colecao = colecao,
            Genero = genero,
            Tamanhos = SepararLista(tamanhos),
            Cores = SepararLista(cores),
            PrecoMinCentavos = precoMin,
            PrecoMaxCentavos = precoMax,
            Busca = busca,
            SomenteDisponiveis = emEstoque ?? true,
            SomenteDestaques = destaque,
            Ordenacao = ordenacao ?? OrdenacaoCatalogo.Relevancia
        };

    /// <summary>Aceita "P,M,G" e tambem o parametro repetido, que o binder concatena com virgula.</summary>
    private static IReadOnlyList<string> SepararLista(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
