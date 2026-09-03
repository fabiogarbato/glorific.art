using Glorific.Application.Common;
using Glorific.Application.DTO;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Common;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Base CRUD de todo servico de agregado.
///
/// TODOS os metodos sao virtual e os pontos de extensao sao protected virtual: o servico
/// concreto sobrescreve o que precisa (filtro de busca, ordenacao, validacao de unicidade,
/// efeito colateral pos-criacao) sem reescrever o fluxo inteiro nem duplicar o SaveChanges.
///
/// Duas regras duras estao materializadas aqui:
/// 1. Quem salva e o CASO DE USO, via IUnitOfWork. O repositorio so registra intencao no
///    ChangeTracker. E por isso que o SaveChangesAsync aparece nesta classe e nao no repositorio.
/// 2. Listagem e sempre paginada, com COUNT separado do SELECT da pagina.
///
/// A materializacao passa por IConsultaAssincrona porque esta camada nao referencia EF —
/// ToListAsync/CountAsync sao extensoes do EF e nao existem aqui.
/// </summary>
public class GenericService<TEntity, TCreate, TUpdate, TResponse>
    : IGenericService<TEntity, TCreate, TUpdate, TResponse>
    where TEntity : BaseEntity
    where TCreate : CreateDto
    where TUpdate : UpdateDto
    where TResponse : ResponseDto
{
    public GenericService(
        IBaseRepository<TEntity> repositorio,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta)
    {
        Repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        Consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    protected IBaseRepository<TEntity> Repositorio { get; }

    protected IUnitOfWork UnitOfWork { get; }

    protected IMapper Mapper { get; }

    protected IConsultaAssincrona Consulta { get; }

    /// <summary>Nome usado na mensagem do 404. Sobrescreva para exibir o rotulo de negocio.</summary>
    protected virtual string NomeEntidade => typeof(TEntity).Name;

    // ------------------------------------------------------------------
    // Pontos de extensao
    // ------------------------------------------------------------------

    /// <summary>
    /// Consulta de partida da listagem. Sem rastreamento por padrao (Query()).
    /// Sobrescreva para trocar por uma query especializada do repositorio concreto — por
    /// exemplo QueryDisponiveis() no catalogo publico.
    /// </summary>
    protected virtual IQueryable<TEntity> ConsultaBase() => Repositorio.Query();

    /// <summary>Filtro da listagem. Aplicado ANTES do COUNT, para o total refletir o filtro.</summary>
    protected virtual IQueryable<TEntity> AplicarFiltro(IQueryable<TEntity> consulta, PageRequest requisicao) => consulta;

    /// <summary>
    /// Ordenacao da pagina. Precisa ser deterministica: sem ORDER BY o Postgres nao garante a
    /// mesma ordem entre duas paginas e a linha 20 reaparece na pagina 2.
    /// </summary>
    protected virtual IQueryable<TEntity> AplicarOrdenacao(IQueryable<TEntity> consulta) =>
        consulta.OrderBy(entidade => entidade.Id);

    protected virtual Task AntesDeCriarAsync(TEntity entidade, TCreate dto, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Roda DEPOIS do SaveChanges: e aqui que a entidade ja tem Id gerado.</summary>
    protected virtual Task DepoisDeCriarAsync(TEntity entidade, TCreate dto, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected virtual Task AntesDeAtualizarAsync(TEntity entidade, TUpdate dto, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected virtual Task DepoisDeAtualizarAsync(TEntity entidade, TUpdate dto, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// Gancho da exclusao. E aqui que o servico concreto troca DELETE por soft delete
    /// (entidade.Ativo = false) ou barra a remocao de registro com historico.
    /// </summary>
    protected virtual Task AntesDeRemoverAsync(TEntity entidade, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    // ------------------------------------------------------------------
    // CRUD
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public virtual async Task<PagedResult<TResponse>> ListarAsync(
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = AplicarFiltro(ConsultaBase(), requisicao);

        // COUNT antes do Skip/Take: Total e a contagem no banco, nunca Items.Count.
        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<TResponse>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = AplicarOrdenacao(consulta)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take);

        var entidades = await Consulta.ListarAsync(pagina, cancellationToken);

        var itens = entidades.Select(Mapear).ToArray();

        return PagedResult<TResponse>.Criar(itens, requisicao, total);
    }

    /// <inheritdoc />
    public virtual async Task<TResponse> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entidade = await ObterOuFalharAsync(id, rastreado: false, cancellationToken);
        return Mapear(entidade);
    }

    /// <inheritdoc />
    public virtual async Task<TResponse> CriarAsync(TCreate dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entidade = Mapper.Map<TEntity>(dto);

        await AntesDeCriarAsync(entidade, dto, cancellationToken);
        await Repositorio.AdicionarAsync(entidade, cancellationToken);

        // Quem salva e o caso de uso. O repositorio acima so registrou a intencao.
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        await DepoisDeCriarAsync(entidade, dto, cancellationToken);

        return Mapear(entidade);
    }

    /// <inheritdoc />
    public virtual async Task<TResponse> AtualizarAsync(
        int id,
        TUpdate dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Rastreado: a entidade vai ser alterada logo em seguida.
        var entidade = await ObterOuFalharAsync(id, rastreado: true, cancellationToken);

        await AntesDeAtualizarAsync(entidade, dto, cancellationToken);

        // Map SOBRE a instancia carregada, e nao Map<TEntity>(dto): criar um objeto novo
        // perderia tudo o que o DTO de update nao carrega (Uuid, DataCriacao, colecoes).
        Mapper.Map(dto, entidade);

        Repositorio.Atualizar(entidade);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        await DepoisDeAtualizarAsync(entidade, dto, cancellationToken);

        return Mapear(entidade);
    }

    /// <inheritdoc />
    public virtual async Task RemoverAsync(int id, CancellationToken cancellationToken = default)
    {
        var entidade = await ObterOuFalharAsync(id, rastreado: true, cancellationToken);

        await AntesDeRemoverAsync(entidade, cancellationToken);

        Repositorio.Remover(entidade);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    protected virtual TResponse Mapear(TEntity entidade) => Mapper.Map<TResponse>(entidade);

    /// <summary>Carrega ou lanca o 404 padrao — evita o if/throw repetido em cada metodo.</summary>
    protected async Task<TEntity> ObterOuFalharAsync(int id, bool rastreado, CancellationToken cancellationToken)
    {
        var entidade = rastreado
            ? await Repositorio.ObterParaEdicaoAsync(id, cancellationToken)
            : await Repositorio.ObterPorIdAsync(id, cancellationToken);

        return entidade ?? throw new EntityNotFoundException(NomeEntidade, id);
    }
}
