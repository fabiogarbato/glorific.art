using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;

namespace Glorific.Application.Services.Interfaces;

public interface IProdutoService
    : IGenericService<Produto, ProdutoCreateDto, ProdutoUpdateDto, ProdutoResponseDto>
{
    // ------------------------------------------------------------------
    // Vitrine publica
    // ------------------------------------------------------------------

    /// <summary>
    /// Vitrine paginada. Por padrao (filtro.SomenteDisponiveis) so entra produto ativo COM ao
    /// menos uma variacao ativa e com saldo livre — mostrar peca que nao pode ser comprada em
    /// nenhum tamanho e descobrir isso no carrinho e o pior caminho possivel.
    /// </summary>
    Task<PagedResult<ProdutoCardDto>> ListarVitrineAsync(
        CatalogoFiltro filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Contagens por categoria, colecao, tamanho, cor e faixa de preco para os filtros.</summary>
    Task<FacetasCatalogoDto> ObterFacetasAsync(
        CatalogoFiltro filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PDP completa por slug: variacoes com saldo, galeria por cor e tabela de medidas.
    /// Produto sem saldo em nenhum tamanho continua abrindo, com Esgotado = true.
    /// </summary>
    Task<ProdutoDetalheDto> ObterDetalhePorSlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Mesma categoria ou mesma colecao, so o que esta disponivel.</summary>
    Task<IReadOnlyList<ProdutoCardDto>> ObterRelacionadosAsync(
        string slug,
        int limite = 8,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------
    // Painel administrativo
    // ------------------------------------------------------------------

    /// <summary>
    /// Listagem do painel, com acesso ao que o filtro global esconde.
    /// <paramref name="ativo"/> null traz ativos e inativos; false e a tela de desativados.
    /// </summary>
    Task<PagedResult<ProdutoResponseDto>> ListarAdminAsync(
        PageRequest requisicao,
        bool? ativo = true,
        int? idCategoria = null,
        string? busca = null,
        CancellationToken cancellationToken = default);

    /// <summary>Detalhe administrativo com variacoes, galeria e colecoes.</summary>
    Task<ProdutoResponseDto> ObterDetalheAdminAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete. Produto NUNCA e apagado: o historico de pedidos depende dele.
    /// Registra LogProduto com o autor da mudanca.
    ///
    /// O autor entra como UUID e nao como id numerico porque e isso que a claim "sub" do token
    /// carrega — pedir o id ao controller o obrigaria a consultar o banco para traduzir.
    /// </summary>
    Task<ProdutoResponseDto> DesativarAsync(
        int id,
        string? uuidUsuario = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reativa e registra o log. Traz de volta o que o filtro global escondia.</summary>
    Task<ProdutoResponseDto> AtivarAsync(
        int id,
        string? uuidUsuario = null,
        CancellationToken cancellationToken = default);

    /// <summary>Auditoria de ativacao/desativacao: quem tirou do ar e quando.</summary>
    Task<PagedResult<ProdutoLogResponseDto>> ObterLogsAsync(
        int idProduto,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);
}
