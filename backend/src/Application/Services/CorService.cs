using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// O swatch de cor e elemento obrigatorio de UI em moda. HexRgb cobre cor solida; a midia de
/// swatch cobre estampa (xadrez, floral), onde uma cor chapada nao representa a peca.
/// </summary>
public class CorService
    : GenericService<Cor, CorCreateDto, CorUpdateDto, CorResponseDto>, ICorService
{
    private readonly ICorRepository _cores;
    private readonly IConsultaCatalogoSemFiltro _semFiltro;

    public CorService(
        ICorRepository cores,
        IConsultaCatalogoSemFiltro semFiltro,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta)
        : base(cores, unitOfWork, mapper, consulta)
    {
        _cores = cores;
        _semFiltro = semFiltro;
    }

    protected override string NomeEntidade => "Cor";

    protected override IQueryable<Cor> AplicarOrdenacao(IQueryable<Cor> consulta) =>
        consulta.OrderBy(c => c.Ordem).ThenBy(c => c.Nome).ThenBy(c => c.Id);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CorResponseDto>> ObterAtivasOrdenadasAsync(
        CancellationToken cancellationToken = default)
    {
        var cores = await _cores.ObterAtivasOrdenadasAsync(cancellationToken);
        return [.. cores.Select(Mapear)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CorResponseDto>> ObterDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        var cores = await _cores.ObterDoProdutoAsync(idProduto, cancellationToken);
        return [.. cores.Select(Mapear)];
    }

    protected override async Task AntesDeCriarAsync(Cor entidade, CorCreateDto dto, CancellationToken cancellationToken)
    {
        entidade.HexRgb = NormalizarHex(dto.HexRgb);

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _cores.SlugEmUsoAsync(candidato, null, ct),
            cancellationToken);
    }

    protected override async Task AntesDeAtualizarAsync(
        Cor entidade,
        CorUpdateDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Slug))
            return;

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _cores.SlugEmUsoAsync(candidato, entidade.Id, ct),
            cancellationToken);
    }

    /// <summary>
    /// Cor com variacao vinculada nao pode ser apagada: a FK e Restrict e vale inclusive para
    /// variacao desativada. Desativar a cor esconde o swatch sem quebrar o historico.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(Cor entidade, CancellationToken cancellationToken)
    {
        // Sem filtro: variacao DESATIVADA continua segurando a FK, e a consulta normal a esconde.
        var emUso = await Consulta.AlgumAsync(
            _semFiltro.Variacoes().Where(v => v.IdCor == entidade.Id),
            cancellationToken);

        BusinessValidationException.LancarSe(
            emUso,
            "Esta cor ja tem variacoes de produto. Desative a cor em vez de remover.");
    }

    /// <summary>Hex sempre em minusculo: "#FFF0E1" e "#fff0e1" pintam a mesma bolinha.</summary>
    private static string NormalizarHex(string hex) =>
        (hex ?? string.Empty).Trim().ToLowerInvariant();
}
