using System.Linq.Expressions;
using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// A PECA do catalogo ("Vestido Midi Linho"), nao a unidade vendavel — quem tem estoque e peso
/// e a ProdutoVariacao.
///
/// Tres regras deste servico merecem destaque:
///
/// 1. PRODUTO NUNCA E DELETADO. Remover vira Ativo = false mais uma linha em logs_produtos.
///    O historico de pedidos aponta para o produto: apagar a linha quebraria todo recibo antigo.
///
/// 2. A VITRINE so mostra produto ativo COM ao menos uma variacao ativa e com saldo livre.
///    Mostrar peca que nao pode ser comprada em nenhum tamanho joga a frustracao para o
///    carrinho. Quando o filtro e afrouxado de proposito (link direto, SEO), a peca aparece com
///    Esgotado = true — sumir a pagina inteira e pior do que exibir o badge.
///
/// 3. O SLUG e SEO-critico e nunca muda sozinho: renomear o produto NAO reescreve o endereco.
/// </summary>
public class ProdutoService
    : GenericService<Produto, ProdutoCreateDto, ProdutoUpdateDto, ProdutoResponseDto>, IProdutoService
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IColecaoRepository _colecoes;
    private readonly ITabelaMedidasRepository _tabelas;
    private readonly IBaseRepository<ProdutoColecao> _vinculosColecao;
    private readonly IBaseRepository<LogProduto> _logs;
    private readonly IUsuarioRepository _usuarios;
    private readonly IConsultaCatalogoSemFiltro _semFiltro;
    private readonly IProdutoVariacaoService _variacoes;
    private readonly IMidiaService _midias;
    private readonly IClock _relogio;

    public ProdutoService(
        IProdutoRepository produtos,
        ICategoriaRepository categorias,
        IColecaoRepository colecoes,
        ITabelaMedidasRepository tabelas,
        IBaseRepository<ProdutoColecao> vinculosColecao,
        IBaseRepository<LogProduto> logs,
        IUsuarioRepository usuarios,
        IConsultaCatalogoSemFiltro semFiltro,
        IProdutoVariacaoService variacoes,
        IMidiaService midias,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
        : base(produtos, unitOfWork, mapper, consulta)
    {
        _produtos = produtos;
        _categorias = categorias;
        _colecoes = colecoes;
        _tabelas = tabelas;
        _vinculosColecao = vinculosColecao;
        _logs = logs;
        _usuarios = usuarios;
        _semFiltro = semFiltro;
        _variacoes = variacoes;
        _midias = midias;
        _relogio = relogio;
    }

    protected override string NomeEntidade => "Produto";

    // ------------------------------------------------------------------
    // Projecoes
    // ------------------------------------------------------------------

    /// <summary>
    /// Card da vitrine. Tudo resolvido no banco: preco minimo, capa, esgotado, swatches e
    /// tamanhos com saldo. Carregar o agregado e calcular em memoria significaria trazer todas
    /// as variacoes de 20 produtos para exibir uma bolinha de cor.
    /// </summary>
    private static readonly Expression<Func<Produto, ProdutoCardDto>> ProjecaoCard =
        produto => new ProdutoCardDto
        {
            Id = produto.Id,
            Nome = produto.Nome,
            Slug = produto.Slug,
            Genero = produto.Genero,
            NomeCategoria = produto.Categoria.Nome,
            SlugCategoria = produto.Categoria.Slug,
            // Cast para int? antes do Min: MIN de conjunto vazio volta NULL do banco, e sem o
            // nullable a materializacao quebraria em produto sem variacao.
            PrecoAPartirDeCentavos = produto.Variacoes
                .Select(v => (int?)(v.PrecoCentavos ?? produto.PrecoBaseCentavos))
                .Min() ?? produto.PrecoBaseCentavos,
            PrecoComparativoCentavos = produto.PrecoComparativoCentavos,
            UrlImagemCapa = produto.Midias
                .OrderByDescending(m => m.EhCapa)
                .ThenBy(m => m.Ordem)
                .ThenBy(m => m.Id)
                .Select(m => m.Midia.Url)
                .FirstOrDefault(),
            AltImagemCapa = produto.Midias
                .OrderByDescending(m => m.EhCapa)
                .ThenBy(m => m.Ordem)
                .ThenBy(m => m.Id)
                .Select(m => m.Midia.AltText)
                .FirstOrDefault(),
            NotaMedia = produto.NotaMedia,
            TotalAvaliacoes = produto.TotalAvaliacoes,
            Destaque = produto.Destaque,
            Esgotado = !produto.Variacoes.Any(v =>
                v.Estoque != null && v.Estoque.Quantidade - v.Estoque.QuantidadeReservada > 0),
            Cores = produto.Variacoes
                .OrderBy(v => v.Cor.Ordem)
                .ThenBy(v => v.Cor.Nome)
                .Select(v => new CorVitrineDto
                {
                    Id = v.Cor.Id,
                    Nome = v.Cor.Nome,
                    Slug = v.Cor.Slug,
                    HexRgb = v.Cor.HexRgb,
                    UrlSwatch = v.Cor.MidiaSwatch == null ? null : v.Cor.MidiaSwatch.Url
                })
                .ToList(),
            TamanhosDisponiveis = produto.Variacoes
                .Where(v => v.Estoque != null && v.Estoque.Quantidade - v.Estoque.QuantidadeReservada > 0)
                .OrderBy(v => v.Tamanho.Ordem)
                .Select(v => new TamanhoVitrineDto
                {
                    Id = v.Tamanho.Id,
                    Codigo = v.Tamanho.Codigo,
                    Ordem = v.Tamanho.Ordem,
                    Grade = v.Tamanho.Grade
                })
                .ToList()
        };

    /// <summary>
    /// Linha da listagem administrativa. Sem variacoes nem galeria: sao 20 linhas por pagina e
    /// o admin quer nome, preco e saldo, nao o agregado inteiro.
    /// </summary>
    private static readonly Expression<Func<Produto, ProdutoResponseDto>> ProjecaoAdmin =
        produto => new ProdutoResponseDto
        {
            Id = produto.Id,
            Nome = produto.Nome,
            Slug = produto.Slug,
            SkuBase = produto.SkuBase,
            Descricao = produto.Descricao,
            IdCategoria = produto.IdCategoria,
            NomeCategoria = produto.Categoria.Nome,
            SlugCategoria = produto.Categoria.Slug,
            Genero = produto.Genero,
            PrecoBaseCentavos = produto.PrecoBaseCentavos,
            PrecoComparativoCentavos = produto.PrecoComparativoCentavos,
            ComposicaoTecido = produto.ComposicaoTecido,
            InstrucoesLavagem = produto.InstrucoesLavagem,
            Modelagem = produto.Modelagem,
            IdTabelaMedidas = produto.IdTabelaMedidas,
            Destaque = produto.Destaque,
            Ativo = produto.Ativo,
            MetaTitle = produto.MetaTitle,
            MetaDescription = produto.MetaDescription,
            NotaMedia = produto.NotaMedia,
            TotalAvaliacoes = produto.TotalAvaliacoes,
            DataCriacao = produto.DataCriacao,
            DataAlteracao = produto.DataAlteracao,
            EstoqueTotalDisponivel = produto.Variacoes
                .Where(v => v.Ativo)
                .Sum(v => v.Estoque == null ? 0 : v.Estoque.Quantidade - v.Estoque.QuantidadeReservada),
            TotalVariacoes = produto.Variacoes.Count(v => v.Ativo)
        };

    private static readonly Func<Produto, ProdutoResponseDto> ProjecaoAdminCompilada = ProjecaoAdmin.Compile();

    /// <summary>
    /// Usado apenas nos caminhos herdados do GenericService. A navegacao de Categoria pode nao
    /// estar carregada — por isso o nome da categoria sai nulo aqui, e nao em excecao.
    /// </summary>
    protected override ProdutoResponseDto Mapear(Produto entidade) =>
        entidade.Categoria is null
            ? MapearSemCategoria(entidade)
            : ProjecaoAdminCompilada(entidade);

    private static ProdutoResponseDto MapearSemCategoria(Produto produto) =>
        new()
        {
            Id = produto.Id,
            Nome = produto.Nome,
            Slug = produto.Slug,
            SkuBase = produto.SkuBase,
            Descricao = produto.Descricao,
            IdCategoria = produto.IdCategoria,
            Genero = produto.Genero,
            PrecoBaseCentavos = produto.PrecoBaseCentavos,
            PrecoComparativoCentavos = produto.PrecoComparativoCentavos,
            ComposicaoTecido = produto.ComposicaoTecido,
            InstrucoesLavagem = produto.InstrucoesLavagem,
            Modelagem = produto.Modelagem,
            IdTabelaMedidas = produto.IdTabelaMedidas,
            Destaque = produto.Destaque,
            Ativo = produto.Ativo,
            MetaTitle = produto.MetaTitle,
            MetaDescription = produto.MetaDescription,
            NotaMedia = produto.NotaMedia,
            TotalAvaliacoes = produto.TotalAvaliacoes,
            DataCriacao = produto.DataCriacao,
            DataAlteracao = produto.DataAlteracao
        };

    // ------------------------------------------------------------------
    // Vitrine publica
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<ProdutoCardDto>> ListarVitrineAsync(
        CatalogoFiltro filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        filtro ??= new CatalogoFiltro();
        requisicao ??= new PageRequest();

        var consulta = MontarConsultaVitrine(filtro);

        // COUNT com o filtro aplicado e antes do Skip/Take.
        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<ProdutoCardDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = Ordenar(consulta, filtro.Ordenacao)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(ProjecaoCard);

        var cards = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<ProdutoCardDto>.Criar([.. cards.Select(RemoverRepeticoes)], requisicao, total);
    }

    /// <inheritdoc />
    public async Task<FacetasCatalogoDto> ObterFacetasAsync(
        CatalogoFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        filtro ??= new CatalogoFiltro();

        var consulta = MontarConsultaVitrine(filtro);

        var totalProdutos = await Consulta.ContarAsync(consulta, cancellationToken);

        if (totalProdutos == 0)
            return new FacetasCatalogoDto();

        var categorias = await Consulta.ListarAsync(
            consulta
                .GroupBy(p => new { p.Categoria.Id, p.Categoria.Nome, p.Categoria.Slug })
                .Select(g => new FacetaItemDto
                {
                    Id = g.Key.Id,
                    Rotulo = g.Key.Nome,
                    Valor = g.Key.Slug,
                    Total = g.Count()
                }),
            cancellationToken);

        var colecoes = await Consulta.ListarAsync(
            consulta
                .SelectMany(p => p.Colecoes)
                .GroupBy(pc => new { pc.Colecao.Id, pc.Colecao.Nome, pc.Colecao.Slug })
                .Select(g => new FacetaItemDto
                {
                    Id = g.Key.Id,
                    Rotulo = g.Key.Nome,
                    Valor = g.Key.Slug,
                    Total = g.Select(pc => pc.IdProduto).Distinct().Count()
                }),
            cancellationToken);

        // Contagem por PRODUTO distinto, nao por variacao: cinco tamanhos da mesma peca sao
        // uma peca so na cabeca de quem esta filtrando.
        var tamanhos = await Consulta.ListarAsync(
            consulta
                .SelectMany(p => p.Variacoes)
                .GroupBy(v => new { v.Tamanho.Id, v.Tamanho.Codigo, v.Tamanho.Ordem })
                // Ordenado no BANCO pela Ordem da grade: o DTO de faceta nao carrega esse campo,
                // e reordenar em memoria depois so daria para ordenar por contagem ou por nome —
                // que colocaria GG antes de P no filtro.
                .OrderBy(g => g.Key.Ordem)
                .ThenBy(g => g.Key.Codigo)
                .Select(g => new FacetaItemDto
                {
                    Id = g.Key.Id,
                    Rotulo = g.Key.Codigo,
                    Valor = g.Key.Codigo,
                    Total = g.Select(v => v.IdProduto).Distinct().Count()
                }),
            cancellationToken);

        var cores = await Consulta.ListarAsync(
            consulta
                .SelectMany(p => p.Variacoes)
                .GroupBy(v => new { v.Cor.Id, v.Cor.Nome, v.Cor.Slug, v.Cor.HexRgb, v.Cor.Ordem })
                .OrderBy(g => g.Key.Ordem)
                .ThenBy(g => g.Key.Nome)
                .Select(g => new FacetaItemDto
                {
                    Id = g.Key.Id,
                    Rotulo = g.Key.Nome,
                    Valor = g.Key.Slug,
                    HexRgb = g.Key.HexRgb,
                    Total = g.Select(v => v.IdProduto).Distinct().Count()
                }),
            cancellationToken);

        // GroupBy com chave constante = agregacao sobre o conjunto inteiro em UMA consulta.
        var faixa = await Consulta.ListarAsync(
            consulta
                .GroupBy(p => 1)
                .Select(g => new { Minimo = g.Min(p => p.PrecoBaseCentavos), Maximo = g.Max(p => p.PrecoBaseCentavos) }),
            cancellationToken);

        var precos = faixa.FirstOrDefault();

        return new FacetasCatalogoDto
        {
            Categorias = [.. categorias.OrderByDescending(f => f.Total).ThenBy(f => f.Rotulo)],
            Colecoes = [.. colecoes.OrderByDescending(f => f.Total).ThenBy(f => f.Rotulo)],
            // Tamanho e cor JA vieram ordenados pela Ordem de exibicao — reordenar aqui por
            // contagem quebraria a sequencia PP, P, M, G, GG do filtro.
            Tamanhos = tamanhos,
            Cores = cores,
            PrecoMinCentavos = precos?.Minimo ?? 0,
            PrecoMaxCentavos = precos?.Maximo ?? 0,
            TotalProdutos = totalProdutos
        };
    }

    /// <inheritdoc />
    public async Task<ProdutoDetalheDto> ObterDetalhePorSlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(slug, "Informe o endereco (slug) do produto.");

        var resumo = await _produtos.ObterPorSlugAsync(slug, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, slug);

        // Uma ida ao banco traz variacoes com tamanho, cor e estoque, galeria ordenada e a
        // tabela de medidas com as linhas. Sem isso a PDP faz N+1 — uma consulta por swatch.
        var produto = await _produtos.ObterCompletoAsync(resumo.Id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, slug);

        var colecoes = await _colecoes.ObterDoProdutoAsync(produto.Id, cancellationToken);

        return MontarDetalhe(produto, colecoes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProdutoCardDto>> ObterRelacionadosAsync(
        string slug,
        int limite = 8,
        CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(slug, "Informe o endereco (slug) do produto.");

        var produto = await _produtos.ObterPorSlugAsync(slug, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, slug);

        var quantidade = limite is < 1 or > 24 ? 8 : limite;

        var idsColecoes = await Consulta.ListarAsync(
            _vinculosColecao.Query().Where(pc => pc.IdProduto == produto.Id).Select(pc => pc.IdColecao),
            cancellationToken);

        var colecoes = idsColecoes.ToArray();

        var consulta = _produtos
            .QueryDisponiveis()
            .Where(p => p.Id != produto.Id
                        && (p.IdCategoria == produto.IdCategoria
                            || p.Colecoes.Any(pc => colecoes.Contains(pc.IdColecao))));

        var cards = await Consulta.ListarAsync(
            consulta
                // Mesma categoria primeiro, depois destaque, depois novidade.
                .OrderByDescending(p => p.IdCategoria == produto.IdCategoria)
                .ThenByDescending(p => p.Destaque)
                .ThenByDescending(p => p.DataCriacao)
                .ThenBy(p => p.Id)
                .Take(quantidade)
                .Select(ProjecaoCard),
            cancellationToken);

        return [.. cards.Select(RemoverRepeticoes)];
    }

    // ------------------------------------------------------------------
    // Painel administrativo
    // ------------------------------------------------------------------

    /// <summary>
    /// A listagem herdada partiria de Repositorio.Query(), que aplica o filtro de soft delete e
    /// nao carrega a categoria. O painel precisa dos dois lados.
    /// </summary>
    public override Task<PagedResult<ProdutoResponseDto>> ListarAsync(
        PageRequest requisicao,
        CancellationToken cancellationToken = default) =>
        ListarAdminAsync(requisicao, ativo: true, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<ProdutoResponseDto>> ListarAdminAsync(
        PageRequest requisicao,
        bool? ativo = true,
        int? idCategoria = null,
        string? busca = null,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = _semFiltro.Produtos();

        if (ativo is not null)
            consulta = consulta.Where(p => p.Ativo == ativo.Value);

        if (idCategoria is not null)
            consulta = consulta.Where(p =>
                p.IdCategoria == idCategoria.Value
                || (p.Categoria.IdCategoriaPai != null && p.Categoria.IdCategoriaPai == idCategoria.Value));

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLowerInvariant();

            consulta = consulta.Where(p =>
                p.Nome.ToLower().Contains(termo)
                || p.SkuBase.ToLower().Contains(termo)
                || p.Slug.ToLower().Contains(termo));
        }

        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<ProdutoResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta
            .OrderByDescending(p => p.DataCriacao)
            .ThenByDescending(p => p.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(ProjecaoAdmin);

        var itens = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<ProdutoResponseDto>.Criar(itens, requisicao, total);
    }

    /// <inheritdoc />
    public override Task<ProdutoResponseDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        ObterDetalheAdminAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<ProdutoResponseDto> ObterDetalheAdminAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var produto = await Consulta.PrimeiroOuPadraoAsync(
            _semFiltro.Produtos().Where(p => p.Id == id).Select(ProjecaoAdmin),
            cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        var variacoes = await _variacoes.ObterPorProdutoAsync(id, incluirInativas: true, cancellationToken);
        var galeria = await _midias.ObterGaleriaAsync(id, cancellationToken);
        var colecoes = await _colecoes.ObterDoProdutoAsync(id, cancellationToken);

        return produto with
        {
            Variacoes = variacoes,
            Midias = galeria,
            Colecoes = [.. colecoes.Select(Mapper.Map<ColecaoResponseDto>)]
        };
    }

    /// <inheritdoc />
    public override async Task<ProdutoResponseDto> CriarAsync(
        ProdutoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var criado = await base.CriarAsync(dto, cancellationToken);
        return await ObterDetalheAdminAsync(criado.Id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<ProdutoResponseDto> AtualizarAsync(
        int id,
        ProdutoUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Sem filtro: o admin precisa conseguir corrigir e completar um produto DESPUBLICADO
        // antes de coloca-lo no ar de novo.
        var produto = await _semFiltro.ObterProdutoParaEdicaoAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        await ValidarVinculosAsync(dto.IdCategoria, dto.IdTabelaMedidas, cancellationToken);
        await GarantirSkuBaseLivreAsync(dto.SkuBase, id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(dto.Slug))
        {
            produto.Slug = await GeradorSlug.UnicoAsync(
                dto.Slug,
                dto.Nome,
                (candidato, ct) => _produtos.SlugEmUsoAsync(candidato, id, ct),
                cancellationToken);
        }

        // O mapeamento ignora Slug, Ativo e as denormalizacoes de avaliacao de proposito.
        Mapper.Map(dto, produto);

        await SincronizarColecoesAsync(id, dto.IdsColecoes, cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterDetalheAdminAsync(id, cancellationToken);
    }

    /// <summary>
    /// DELETE do painel e SOFT delete. Chamar Remover no repositorio apagaria a linha que o
    /// historico de pedidos referencia.
    /// </summary>
    public override async Task RemoverAsync(int id, CancellationToken cancellationToken = default) =>
        await DesativarAsync(id, null, cancellationToken);

    /// <inheritdoc />
    public Task<ProdutoResponseDto> DesativarAsync(
        int id,
        string? uuidUsuario = null,
        CancellationToken cancellationToken = default) =>
        AlternarAtivoAsync(id, ativo: false, uuidUsuario, cancellationToken);

    /// <inheritdoc />
    public Task<ProdutoResponseDto> AtivarAsync(
        int id,
        string? uuidUsuario = null,
        CancellationToken cancellationToken = default) =>
        AlternarAtivoAsync(id, ativo: true, uuidUsuario, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<ProdutoLogResponseDto>> ObterLogsAsync(
        int idProduto,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = _logs.Query().Where(l => l.IdProduto == idProduto);

        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<ProdutoLogResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta
            .OrderByDescending(l => l.DataAlteracao)
            .ThenByDescending(l => l.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(l => new ProdutoLogResponseDto
            {
                Id = l.Id,
                IdProduto = l.IdProduto,
                AtivoAntigo = l.AtivoAntigo,
                AtivoNovo = l.AtivoNovo,
                IdUsuario = l.IdUsuario,
                NomeUsuario = l.Usuario == null ? null : l.Usuario.NomeCompleto,
                DataAlteracao = l.DataAlteracao
            });

        var itens = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<ProdutoLogResponseDto>.Criar(itens, requisicao, total);
    }

    // ------------------------------------------------------------------
    // Ganchos do CRUD generico
    // ------------------------------------------------------------------

    protected override async Task AntesDeCriarAsync(
        Produto entidade,
        ProdutoCreateDto dto,
        CancellationToken cancellationToken)
    {
        await ValidarVinculosAsync(dto.IdCategoria, dto.IdTabelaMedidas, cancellationToken);
        await GarantirSkuBaseLivreAsync(dto.SkuBase, null, cancellationToken);

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _produtos.SlugEmUsoAsync(candidato, null, ct),
            cancellationToken);
    }

    /// <summary>Roda depois do SaveChanges: e aqui que o produto ja tem Id para os vinculos.</summary>
    protected override async Task DepoisDeCriarAsync(
        Produto entidade,
        ProdutoCreateDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.IdsColecoes.Count == 0)
            return;

        await SincronizarColecoesAsync(entidade.Id, dto.IdsColecoes, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Ativa ou desativa registrando LogProduto. Os dois lados passam por aqui porque a
    /// pergunta que a auditoria responde e "quem tirou isso do ar", e reativacao sem log deixa
    /// a linha do tempo com um buraco.
    /// </summary>
    private async Task<ProdutoResponseDto> AlternarAtivoAsync(
        int id,
        bool ativo,
        string? uuidUsuario,
        CancellationToken cancellationToken)
    {
        var produto = await _semFiltro.ObterProdutoParaEdicaoAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        if (produto.Ativo == ativo)
            return await ObterDetalheAdminAsync(id, cancellationToken);

        var anterior = produto.Ativo;
        produto.Ativo = ativo;

        // Autor desconhecido nao invalida a auditoria: a coluna e nullable e "quando" ja e
        // metade da resposta. Falhar aqui bloquearia a despublicacao por um detalhe de log.
        var autor = string.IsNullOrWhiteSpace(uuidUsuario)
            ? null
            : await _usuarios.ObterPorUuidAsync(uuidUsuario, cancellationToken);

        await _logs.AdicionarAsync(
            new LogProduto
            {
                IdProduto = id,
                AtivoAntigo = anterior,
                AtivoNovo = ativo,
                IdUsuario = autor?.Id,
                // LogProduto nao e IAuditable: o carimbo vem do IClock, nunca de DateTime.UtcNow.
                DataAlteracao = _relogio.UtcNow
            },
            cancellationToken);

        // Produto e log na MESMA unidade de trabalho: ou os dois acontecem, ou nenhum.
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterDetalheAdminAsync(id, cancellationToken);
    }

    private IQueryable<Produto> MontarConsultaVitrine(CatalogoFiltro filtro)
    {
        // QueryDisponiveis ja exige variacao ativa COM saldo livre. Quando o filtro e afrouxado,
        // a exigencia cai para "tem ao menos uma variacao ativa" — sem isso a vitrine mostraria
        // cabide vazio, produto sem nenhum SKU cadastrado.
        var consulta = filtro.SomenteDisponiveis
            ? _produtos.QueryDisponiveis()
            : _produtos.Query().Where(p => p.Variacoes.Any());

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            var categoria = filtro.Categoria.Trim().ToLowerInvariant();

            // Categoria pai traz as filhas junto: /vestidos precisa listar "Vestidos > Midi".
            consulta = consulta.Where(p =>
                p.Categoria.Slug == categoria
                || (p.Categoria.CategoriaPai != null && p.Categoria.CategoriaPai.Slug == categoria));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Colecao))
        {
            var colecao = filtro.Colecao.Trim().ToLowerInvariant();
            consulta = consulta.Where(p => p.Colecoes.Any(pc => pc.Colecao.Slug == colecao));
        }

        if (filtro.Genero is not null)
            consulta = consulta.Where(p => p.Genero == filtro.Genero.Value);

        if (filtro.SomenteDestaques == true)
            consulta = consulta.Where(p => p.Destaque);

        if (filtro.Tamanhos.Count > 0)
        {
            var codigos = filtro.Tamanhos
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (codigos.Length > 0)
            {
                // O tamanho precisa estar DISPONIVEL, nao apenas cadastrado: filtrar por "GG" e
                // receber peca sem GG em estoque e o mesmo que nao ter filtro.
                consulta = consulta.Where(p => p.Variacoes.Any(v =>
                    codigos.Contains(v.Tamanho.Codigo)
                    && v.Estoque != null
                    && v.Estoque.Quantidade - v.Estoque.QuantidadeReservada > 0));
            }
        }

        if (filtro.Cores.Count > 0)
        {
            var slugs = filtro.Cores
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (slugs.Length > 0)
                consulta = consulta.Where(p => p.Variacoes.Any(v => slugs.Contains(v.Cor.Slug)));
        }

        // Faixa de preco pelo preco EFETIVO da variacao: filtrar so pelo preco base erraria em
        // todo produto que tem override de preco por tamanho.
        if (filtro.PrecoMinCentavos is not null)
        {
            var minimo = filtro.PrecoMinCentavos.Value;
            consulta = consulta.Where(p => p.Variacoes.Any(v => (v.PrecoCentavos ?? p.PrecoBaseCentavos) >= minimo));
        }

        if (filtro.PrecoMaxCentavos is not null)
        {
            var maximo = filtro.PrecoMaxCentavos.Value;
            consulta = consulta.Where(p => p.Variacoes.Any(v => (v.PrecoCentavos ?? p.PrecoBaseCentavos) <= maximo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = filtro.Busca.Trim().ToLowerInvariant();

            // ToLower dos dois lados: o Postgres compara com case sensitivity por padrao e uma
            // busca por "linho" nao acharia "Linho".
            consulta = consulta.Where(p =>
                p.Nome.ToLower().Contains(termo)
                || p.SkuBase.ToLower().Contains(termo)
                || (p.Descricao != null && p.Descricao.ToLower().Contains(termo)));
        }

        return consulta;
    }

    /// <summary>
    /// Ordenacao SEMPRE deterministica: sem desempate por Id o Postgres nao garante a mesma
    /// ordem entre duas paginas e a mesma peca reaparece na pagina seguinte.
    /// </summary>
    private static IQueryable<Produto> Ordenar(IQueryable<Produto> consulta, OrdenacaoCatalogo ordenacao) =>
        ordenacao switch
        {
            OrdenacaoCatalogo.PrecoCrescente =>
                consulta.OrderBy(p => p.PrecoBaseCentavos).ThenBy(p => p.Id),

            OrdenacaoCatalogo.PrecoDecrescente =>
                consulta.OrderByDescending(p => p.PrecoBaseCentavos).ThenBy(p => p.Id),

            OrdenacaoCatalogo.Novidade =>
                consulta.OrderByDescending(p => p.DataCriacao).ThenByDescending(p => p.Id),

            OrdenacaoCatalogo.MaisAvaliados =>
                consulta.OrderByDescending(p => p.NotaMedia ?? 0m)
                    .ThenByDescending(p => p.TotalAvaliacoes)
                    .ThenBy(p => p.Id),

            _ => consulta.OrderByDescending(p => p.Destaque)
                .ThenByDescending(p => p.DataCriacao)
                .ThenBy(p => p.Id)
        };

    /// <summary>
    /// A projecao traz uma cor e um tamanho POR VARIACAO — a mesma cor aparece uma vez por
    /// tamanho. Deduplicar no banco exigiria Distinct sobre subconsulta projetada, que o
    /// provedor traduz mal; sao poucos itens por card e a limpeza sai barata aqui.
    /// </summary>
    private static ProdutoCardDto RemoverRepeticoes(ProdutoCardDto card) =>
        card with
        {
            Cores = [.. card.Cores.DistinctBy(c => c.Id)],
            TamanhosDisponiveis = [.. card.TamanhosDisponiveis.DistinctBy(t => t.Id).OrderBy(t => t.Ordem)]
        };

    private ProdutoDetalheDto MontarDetalhe(Produto produto, IReadOnlyList<Colecao> colecoes)
    {
        var variacoes = produto.Variacoes
            .OrderBy(v => v.Cor.Ordem)
            .ThenBy(v => v.Cor.Nome)
            .ThenBy(v => v.Tamanho.Ordem)
            .Select(v =>
            {
                var disponivel = v.Estoque is null ? 0 : v.Estoque.Quantidade - v.Estoque.QuantidadeReservada;

                return new VariacaoVitrineDto
                {
                    Id = v.Id,
                    Sku = v.Sku,
                    IdTamanho = v.IdTamanho,
                    CodigoTamanho = v.Tamanho.Codigo,
                    OrdemTamanho = v.Tamanho.Ordem,
                    IdCor = v.IdCor,
                    NomeCor = v.Cor.Nome,
                    SlugCor = v.Cor.Slug,
                    HexRgb = v.Cor.HexRgb,
                    PrecoCentavos = v.PrecoCentavos ?? produto.PrecoBaseCentavos,
                    QuantidadeDisponivel = disponivel < 0 ? 0 : disponivel,
                    Disponivel = disponivel > 0
                };
            })
            .ToArray();

        // A galeria vem sem a navegacao de Cor carregada (o Include da PDP nao a traz, e trazer
        // significaria uma juncao a mais em toda abertura de pagina). O slug sai das variacoes,
        // que ja estao em memoria e sao a fonte das cores exibidas no seletor.
        var slugPorCor = variacoes
            .DistinctBy(v => v.IdCor)
            .ToDictionary(v => v.IdCor, v => v.SlugCor);

        var galeria = produto.Midias
            .GroupBy(m => m.IdCor)
            .Select(grupo => new GaleriaCorDto
            {
                IdCor = grupo.Key,
                SlugCor = grupo.Key is not null && slugPorCor.TryGetValue(grupo.Key.Value, out var slugCor)
                    ? slugCor
                    : null,
                Midias =
                [
                    .. grupo
                        .OrderByDescending(m => m.EhCapa)
                        .ThenBy(m => m.Ordem)
                        .ThenBy(m => m.Id)
                        .Select(m => new MidiaVitrineDto
                        {
                            Id = m.IdMidia,
                            Url = m.Midia.Url,
                            AltText = m.Midia.AltText,
                            Largura = m.Midia.Largura,
                            Altura = m.Midia.Altura,
                            Ordem = m.Ordem,
                            EhCapa = m.EhCapa
                        })
                ]
            })
            // Grupo neutro (sem cor) primeiro: e o que a PDP mostra antes de qualquer swatch
            // ser escolhido.
            .OrderBy(g => g.IdCor.HasValue)
            .ThenBy(g => g.IdCor)
            .ToArray();

        var cores = variacoes
            .DistinctBy(v => v.IdCor)
            .Select(v => new CorVitrineDto
            {
                Id = v.IdCor,
                Nome = v.NomeCor,
                Slug = v.SlugCor,
                HexRgb = v.HexRgb
            })
            .ToArray();

        var tamanhos = produto.Variacoes
            .Select(v => v.Tamanho)
            .DistinctBy(t => t.Id)
            .OrderBy(t => t.Ordem)
            .Select(t => new TamanhoVitrineDto
            {
                Id = t.Id,
                Codigo = t.Codigo,
                Ordem = t.Ordem,
                Grade = t.Grade
            })
            .ToArray();

        return new ProdutoDetalheDto
        {
            Id = produto.Id,
            Nome = produto.Nome,
            Slug = produto.Slug,
            SkuBase = produto.SkuBase,
            Descricao = produto.Descricao,
            Genero = produto.Genero,
            IdCategoria = produto.IdCategoria,
            NomeCategoria = produto.Categoria?.Nome,
            SlugCategoria = produto.Categoria?.Slug,
            PrecoBaseCentavos = produto.PrecoBaseCentavos,
            PrecoAPartirDeCentavos = variacoes.Length == 0
                ? produto.PrecoBaseCentavos
                : variacoes.Min(v => v.PrecoCentavos),
            PrecoComparativoCentavos = produto.PrecoComparativoCentavos,
            ComposicaoTecido = produto.ComposicaoTecido,
            InstrucoesLavagem = produto.InstrucoesLavagem,
            Modelagem = produto.Modelagem,
            NotaMedia = produto.NotaMedia,
            TotalAvaliacoes = produto.TotalAvaliacoes,
            // Badge de esgotado: a peca existe, mas nenhum tamanho tem saldo livre.
            Esgotado = !variacoes.Any(v => v.Disponivel),
            MetaTitle = produto.MetaTitle,
            MetaDescription = produto.MetaDescription,
            Cores = cores,
            Tamanhos = tamanhos,
            Variacoes = variacoes,
            Galeria = galeria,
            TabelaMedidas = produto.TabelaMedidas is null
                ? null
                : Mapper.Map<TabelaMedidasResponseDto>(produto.TabelaMedidas),
            Colecoes = [.. colecoes.Select(Mapper.Map<ColecaoResponseDto>)]
        };
    }

    private async Task ValidarVinculosAsync(
        int idCategoria,
        int? idTabelaMedidas,
        CancellationToken cancellationToken)
    {
        if (!await _categorias.ExisteAsync(idCategoria, cancellationToken))
            throw new BusinessValidationException("A categoria informada nao existe.");

        if (idTabelaMedidas is null)
            return;

        if (!await _tabelas.ExisteAsync(idTabelaMedidas.Value, cancellationToken))
            throw new BusinessValidationException("A tabela de medidas informada nao existe.");
    }

    /// <summary>
    /// SKU base e digitado a mao a partir da planilha do fornecedor: colidir e erro do operador,
    /// nao algo para o sistema resolver sozinho inventando um sufixo.
    /// </summary>
    private async Task GarantirSkuBaseLivreAsync(string skuBase, int? idIgnorar, CancellationToken cancellationToken)
    {
        var normalizado = (skuBase ?? string.Empty).Trim().ToUpperInvariant();

        BusinessValidationException.LancarSeVazio(normalizado, "Informe o SKU base do produto.");

        // IgnoreQueryFilters no repositorio: produto desativado ainda ocupa o SKU no indice unico.
        var emUso = await _produtos.SkuBaseEmUsoAsync(normalizado, idIgnorar, cancellationToken);

        BusinessValidationException.LancarSe(
            emUso,
            $"Ja existe um produto com o SKU base '{normalizado}'.");
    }

    /// <summary>
    /// Null NAO mexe nos vinculos; lista vazia REMOVE todos. Tratar os dois como "vazio" faria
    /// qualquer edicao de preco apagar a curadoria das colecoes sem ninguem pedir.
    /// </summary>
    private async Task SincronizarColecoesAsync(
        int idProduto,
        IReadOnlyList<int>? idsColecoes,
        CancellationToken cancellationToken)
    {
        if (idsColecoes is null)
            return;

        var desejados = idsColecoes.Distinct().ToArray();

        if (desejados.Length > 0)
        {
            var existentes = await _colecoes.ObterPorIdsAsync(desejados, cancellationToken);

            var faltantes = desejados.Except(existentes.Select(c => c.Id)).ToArray();

            BusinessValidationException.LancarSe(
                faltantes.Length > 0,
                $"Colecao(oes) inexistente(s): {string.Join(", ", faltantes)}.");
        }

        var atuais = await Consulta.ListarAsync(
            _vinculosColecao.Query().Where(pc => pc.IdProduto == idProduto).Select(pc => pc.IdColecao),
            cancellationToken);

        foreach (var remover in atuais.Except(desejados))
            await _colecoes.DesvincularProdutoAsync(remover, idProduto, cancellationToken);

        for (var posicao = 0; posicao < desejados.Length; posicao++)
            await _colecoes.VincularProdutoAsync(desejados[posicao], idProduto, posicao, cancellationToken);
    }
}
