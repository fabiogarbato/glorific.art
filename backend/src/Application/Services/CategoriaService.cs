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
/// Taxonomia do catalogo. Auto-relacao de UM nivel ("Vestidos" &gt; "Midi"), e o servico e quem
/// garante que continue sendo um nivel: sem essa trava, uma arvore de tres niveis quebra o menu
/// e a listagem por categoria pai deixa de encontrar os produtos dos netos.
/// </summary>
public class CategoriaService
    : GenericService<Categoria, CategoriaCreateDto, CategoriaUpdateDto, CategoriaResponseDto>, ICategoriaService
{
    private readonly ICategoriaRepository _categorias;

    public CategoriaService(
        ICategoriaRepository categorias,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta)
        : base(categorias, unitOfWork, mapper, consulta)
    {
        _categorias = categorias;
    }

    protected override string NomeEntidade => "Categoria";

    /// <summary>Ordem de EXIBICAO do menu, nunca a ordem de insercao.</summary>
    protected override IQueryable<Categoria> AplicarOrdenacao(IQueryable<Categoria> consulta) =>
        consulta.OrderBy(c => c.Ordem).ThenBy(c => c.Nome).ThenBy(c => c.Id);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoriaResponseDto>> ObterArvoreAsync(
        bool somenteHabilitadas = true,
        CancellationToken cancellationToken = default)
    {
        var raizes = await _categorias.ObterArvoreAsync(somenteHabilitadas, cancellationToken);
        return [.. raizes.Select(Mapear)];
    }

    /// <inheritdoc />
    public async Task<CategoriaResponseDto> ObterPorSlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(slug, "Informe o endereco (slug) da categoria.");

        var categoria = await _categorias.ObterPorSlugAsync(slug, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, slug);

        return Mapear(categoria);
    }

    protected override async Task AntesDeCriarAsync(
        Categoria entidade,
        CategoriaCreateDto dto,
        CancellationToken cancellationToken)
    {
        await ValidarPaiAsync(dto.IdCategoriaPai, idAtual: null, cancellationToken);

        entidade.Slug = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _categorias.SlugEmUsoAsync(candidato, null, ct),
            cancellationToken);
    }

    protected override async Task AntesDeAtualizarAsync(
        Categoria entidade,
        CategoriaUpdateDto dto,
        CancellationToken cancellationToken)
    {
        BusinessValidationException.LancarSe(
            dto.IdCategoriaPai == entidade.Id,
            "Uma categoria nao pode ser pai dela mesma.");

        await ValidarPaiAsync(dto.IdCategoriaPai, entidade.Id, cancellationToken);

        // Slug so muda quando o admin escreve um diferente. Renomear a categoria NAO reescreve o
        // endereco: link indexado e compartilhado continuaria apontando para 404.
        if (string.IsNullOrWhiteSpace(dto.Slug))
            return;

        var normalizado = await GeradorSlug.UnicoAsync(
            dto.Slug,
            dto.Nome,
            (candidato, ct) => _categorias.SlugEmUsoAsync(candidato, entidade.Id, ct),
            cancellationToken);

        entidade.Slug = normalizado;
    }

    /// <summary>
    /// Remocao real, e nao soft delete: categoria nao aparece em historico de pedido. Mas so
    /// quando esta livre — o Restrict do banco viraria erro cru de FK na tela do admin.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(Categoria entidade, CancellationToken cancellationToken)
    {
        var possuiVinculos = await _categorias.PossuiVinculosAsync(entidade.Id, cancellationToken);

        BusinessValidationException.LancarSe(
            possuiVinculos,
            "Esta categoria tem subcategorias ou produtos vinculados (inclusive produtos desativados). " +
            "Mova-os para outra categoria antes de remover.");
    }

    /// <summary>
    /// A arvore tem UM nivel: o pai precisa existir e nao pode, ele proprio, ter pai.
    /// Sem esta trava o menu ganha um terceiro nivel que nenhuma tela sabe desenhar.
    /// </summary>
    private async Task ValidarPaiAsync(int? idPai, int? idAtual, CancellationToken cancellationToken)
    {
        if (idPai is null)
            return;

        BusinessValidationException.LancarSe(
            idPai == idAtual,
            "Uma categoria nao pode ser pai dela mesma.");

        var pai = await _categorias.ObterPorIdAsync(idPai.Value, cancellationToken)
            ?? throw new BusinessValidationException("A categoria pai informada nao existe.");

        BusinessValidationException.LancarSe(
            pai.IdCategoriaPai is not null,
            "A arvore de categorias tem apenas um nivel: escolha uma categoria raiz como pai.");
    }
}
