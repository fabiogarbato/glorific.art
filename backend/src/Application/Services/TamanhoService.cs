using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Tamanho como ENTIDADE, nunca string. A coluna Ordem e o motivo de existir: sem ela "GG"
/// ordena antes de "P" alfabeticamente e o seletor da pagina de produto sai errado.
/// </summary>
public class TamanhoService
    : GenericService<Tamanho, TamanhoCreateDto, TamanhoUpdateDto, TamanhoResponseDto>, ITamanhoService
{
    private readonly ITamanhoRepository _tamanhos;
    private readonly IConsultaCatalogoSemFiltro _semFiltro;

    public TamanhoService(
        ITamanhoRepository tamanhos,
        IConsultaCatalogoSemFiltro semFiltro,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta)
        : base(tamanhos, unitOfWork, mapper, consulta)
    {
        _tamanhos = tamanhos;
        _semFiltro = semFiltro;
    }

    protected override string NomeEntidade => "Tamanho";

    protected override IQueryable<Tamanho> AplicarOrdenacao(IQueryable<Tamanho> consulta) =>
        consulta.OrderBy(t => t.Grade).ThenBy(t => t.Ordem).ThenBy(t => t.Id);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TamanhoResponseDto>> ObterAtivosOrdenadosAsync(
        GradeTamanho? grade = null,
        CancellationToken cancellationToken = default)
    {
        var tamanhos = await _tamanhos.ObterAtivosOrdenadosAsync(grade, cancellationToken);
        return [.. tamanhos.Select(Mapear)];
    }

    protected override async Task AntesDeCriarAsync(
        Tamanho entidade,
        TamanhoCreateDto dto,
        CancellationToken cancellationToken)
    {
        entidade.Codigo = NormalizarCodigo(dto.Codigo);

        await GarantirCodigoLivreAsync(dto.Grade, entidade.Codigo, null, cancellationToken);
    }

    protected override async Task AntesDeAtualizarAsync(
        Tamanho entidade,
        TamanhoUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var codigo = NormalizarCodigo(dto.Codigo);

        await GarantirCodigoLivreAsync(dto.Grade, codigo, entidade.Id, cancellationToken);
    }

    /// <summary>
    /// Remocao real: tamanho nao aparece em historico. Mas com variacao vinculada o Restrict do
    /// banco viraria erro cru — inclusive por variacao DESATIVADA, que a FK continua segurando.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(Tamanho entidade, CancellationToken cancellationToken)
    {
        // Sem filtro: variacao DESATIVADA continua segurando a FK, e a consulta normal a esconde.
        var emUso = await Consulta.AlgumAsync(
            _semFiltro.Variacoes().Where(v => v.IdTamanho == entidade.Id),
            cancellationToken);

        BusinessValidationException.LancarSe(
            emUso,
            "Este tamanho ja tem variacoes de produto. Desative o tamanho em vez de remover.");
    }

    /// <summary>Codigo de tamanho e sempre maiusculo e sem espaco: "p " e "P" sao o mesmo item.</summary>
    private static string NormalizarCodigo(string codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// O codigo so e unico DENTRO da grade: "38" existe na numerica e na infantil, e sao
    /// tamanhos diferentes. Por isso a checagem leva sempre as duas partes da chave.
    /// </summary>
    private async Task GarantirCodigoLivreAsync(
        GradeTamanho grade,
        string codigo,
        int? idIgnorar,
        CancellationToken cancellationToken)
    {
        BusinessValidationException.LancarSeVazio(codigo, "Informe o codigo do tamanho.");

        var emUso = await _tamanhos.CodigoEmUsoAsync(grade, codigo, idIgnorar, cancellationToken);

        BusinessValidationException.LancarSe(
            emUso,
            $"Ja existe o tamanho '{codigo}' na grade {grade}.");
    }
}
