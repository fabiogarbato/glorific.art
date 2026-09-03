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
/// Curadoria temporal ("Capsula Advento", "Linha Salmos"). E ortogonal a categoria: a mesma peca
/// esta na categoria "Vestidos" E na colecao "Advento".
/// </summary>
public class ColecaoService
    : GenericService<Colecao, ColecaoCreateDto, ColecaoUpdateDto, ColecaoResponseDto>, IColecaoService
{
    private readonly IColecaoRepository _colecoes;
    private readonly IProdutoRepository _produtos;
    private readonly IBaseRepository<ProdutoColecao> _vinculos;
    private readonly IClock _relogio;

    public ColecaoService(
        IColecaoRepository colecoes,
        IProdutoRepository produtos,
        IBaseRepository<ProdutoColecao> vinculos,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
        : base(colecoes, unitOfWork, mapper, consulta)
    {
        _colecoes = colecoes;
        _produtos = produtos;
        _vinculos = vinculos;
        _relogio = relogio;
    }

    protected override string NomeEntidade => "Colecao";

    protected override IQueryable<Colecao> AplicarOrdenacao(IQueryable<Colecao> consulta) =>
        consulta
            .OrderByDescending(c => c.Destaque)
            .ThenBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ThenBy(c => c.Id);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ColecaoResponseDto>> ObterVigentesAsync(
        CancellationToken cancellationToken = default)
    {
        // O "agora" vem do IClock: sem ele nao da para testar a virada de um drop agendado sem
        // esperar o relogio andar.
        var vigentes = await _colecoes.ObterVigentesAsync(_relogio.UtcNow, cancellationToken);
        return [.. vigentes.Select(Mapear)];
    }

    /// <inheritdoc />
    public async Task<ColecaoResponseDto> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(slug, "Informe o endereco (slug) da colecao.");

        var colecao = await _colecoes.ObterPorSlugAsync(slug, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, slug);

        return Mapear(colecao);
    }

    /// <inheritdoc />
    public async Task VincularProdutoAsync(
        int idColecao,
        VincularProdutoColecaoDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _colecoes.ExisteAsync(idColecao, cancellationToken))
            throw new EntityNotFoundException(NomeEntidade, idColecao);

        if (!await _produtos.ExisteAsync(dto.IdProduto, cancellationToken))
            throw new EntityNotFoundException("Produto", dto.IdProduto);

        await _colecoes.VincularProdutoAsync(idColecao, dto.IdProduto, dto.Ordem, cancellationToken);

        // Quem salva e o caso de uso. O repositorio acima so registrou a intencao.
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DesvincularProdutoAsync(
        int idColecao,
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        if (!await _colecoes.ExisteAsync(idColecao, cancellationToken))
            throw new EntityNotFoundException(NomeEntidade, idColecao);

        await _colecoes.DesvincularProdutoAsync(idColecao, idProduto, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    protected override async Task AntesDeCriarAsync(
        Colecao entidade,
        ColecaoCreateDto dto,
        CancellationToken cancellationToken)
    {
        ValidarJanela(dto.DataInicio, dto.DataFim);

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _colecoes.SlugEmUsoAsync(candidato, null, ct),
            cancellationToken);
    }

    protected override async Task AntesDeAtualizarAsync(
        Colecao entidade,
        ColecaoUpdateDto dto,
        CancellationToken cancellationToken)
    {
        ValidarJanela(dto.DataInicio, dto.DataFim);

        if (string.IsNullOrWhiteSpace(dto.Slug))
            return;

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _colecoes.SlugEmUsoAsync(candidato, entidade.Id, ct),
            cancellationToken);
    }

    /// <summary>
    /// A FK de produtos_colecoes para colecoes e Restrict: apagar uma colecao com produtos
    /// vinculados devolveria violacao crua de FK na tela do admin. Aqui vira mensagem que diz o
    /// que fazer. Colecao e curadoria — desvincular nunca apaga o produto.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(Colecao entidade, CancellationToken cancellationToken)
    {
        var temProdutos = await Consulta.AlgumAsync(
            _vinculos.Query().Where(pc => pc.IdColecao == entidade.Id),
            cancellationToken);

        BusinessValidationException.LancarSe(
            temProdutos,
            "Esta colecao ainda tem produtos vinculados. Desvincule os produtos antes de remover.");
    }

    /// <summary>
    /// Janela invertida publica um drop que nunca entra no ar e ninguem descobre ate o cliente
    /// reclamar que a colecao "sumiu". Barrar aqui e mais barato que investigar depois.
    /// </summary>
    private static void ValidarJanela(DateTime? inicio, DateTime? fim)
    {
        BusinessValidationException.LancarSe(
            inicio is not null && fim is not null && fim < inicio,
            "A data final da colecao nao pode ser anterior a data inicial.");
    }
}
