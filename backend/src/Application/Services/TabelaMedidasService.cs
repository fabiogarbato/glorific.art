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
/// Guia de medidas — o item numero 1 de reducao de devolucao em moda.
///
/// As linhas sao substituidas EM BLOCO na edicao. Diferenca item a item exigiria id de linha
/// vindo do navegador e deixaria linha orfa quando o admin retira um tamanho da grade; uma
/// tabela de medidas tem seis linhas, reescrever o bloco e mais simples e nao deixa lixo.
/// </summary>
public class TabelaMedidasService
    : GenericService<TabelaMedidas, TabelaMedidasCreateDto, TabelaMedidasUpdateDto, TabelaMedidasResponseDto>,
      ITabelaMedidasService
{
    private readonly ITabelaMedidasRepository _tabelas;
    private readonly ITamanhoRepository _tamanhos;
    private readonly IBaseRepository<TabelaMedidasLinha> _linhas;
    private readonly IClock _relogio;

    public TabelaMedidasService(
        ITabelaMedidasRepository tabelas,
        ITamanhoRepository tamanhos,
        IBaseRepository<TabelaMedidasLinha> linhas,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
        : base(tabelas, unitOfWork, mapper, consulta)
    {
        _tabelas = tabelas;
        _tamanhos = tamanhos;
        _linhas = linhas;
        _relogio = relogio;
    }

    protected override string NomeEntidade => "Tabela de medidas";

    protected override IQueryable<TabelaMedidas> AplicarOrdenacao(IQueryable<TabelaMedidas> consulta) =>
        consulta.OrderBy(t => t.Nome).ThenBy(t => t.Id);

    /// <inheritdoc />
    public async Task<TabelaMedidasResponseDto> ObterComLinhasAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var tabela = await _tabelas.ObterComLinhasAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        return Mapear(tabela);
    }

    /// <inheritdoc />
    public override async Task<TabelaMedidasResponseDto> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        // Tabela sem as linhas nao serve para nada: o detalhe SEMPRE vem completo.
        await ObterComLinhasAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TabelaMedidasPublicaDto>> ListarPublicasAsync(
        CancellationToken cancellationToken = default)
    {
        var tabelas = await _tabelas.ListarAtivasComLinhasAsync(cancellationToken);

        return [.. tabelas.Select(MapearPublica)];
    }

    /// <inheritdoc />
    public async Task<TabelaMedidasPublicaDto> ObterPublicaAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var tabela = await _tabelas.ObterAtivaComLinhasAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        return MapearPublica(tabela);
    }

    /// <summary>
    /// Projecao publica escrita a mao, sem Mapster.
    ///
    /// De proposito: e o unico jeito de garantir que um campo novo acrescentado ao agregado NAO
    /// aparece sozinho na resposta publica. Mapeamento por convencao faz o contrario — ele adota
    /// o campo novo em silencio, e o vazamento so e descoberto por quem le a resposta.
    /// </summary>
    private static TabelaMedidasPublicaDto MapearPublica(TabelaMedidas tabela) =>
        new()
        {
            Id = tabela.Id,
            Nome = tabela.Nome,
            Observacao = tabela.Observacao,
            Linhas =
            [
                .. tabela.Linhas
                    // A ordenacao ja vem do banco; repetida aqui porque uma tabela montada em
                    // memoria (teste, cache futuro) nao teria essa garantia.
                    .OrderBy(linha => linha.Ordem)
                    .ThenBy(linha => linha.Id)
                    .Select(linha => new TabelaMedidasLinhaPublicaDto
                    {
                        IdTamanho = linha.IdTamanho,
                        CodigoTamanho = linha.Tamanho?.Codigo ?? string.Empty,
                        OrdemTamanho = linha.Ordem,
                        BustoCm = linha.BustoCm,
                        CinturaCm = linha.CinturaCm,
                        QuadrilCm = linha.QuadrilCm,
                        ComprimentoCm = linha.ComprimentoCm,
                        MangaCm = linha.MangaCm
                    })
            ]
        };

    /// <summary>
    /// Recarrega depois de salvar: as linhas recem-criadas ainda nao tem a navegacao de Tamanho
    /// preenchida, e devolver a tabela sem o codigo do tamanho faria a tela mostrar coluna vazia.
    /// </summary>
    public override async Task<TabelaMedidasResponseDto> CriarAsync(
        TabelaMedidasCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var criada = await base.CriarAsync(dto, cancellationToken);
        return await ObterComLinhasAsync(criada.Id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TabelaMedidasResponseDto> AtualizarAsync(
        int id,
        TabelaMedidasUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        await base.AtualizarAsync(id, dto, cancellationToken);
        return await ObterComLinhasAsync(id, cancellationToken);
    }

    protected override async Task AntesDeCriarAsync(
        TabelaMedidas entidade,
        TabelaMedidasCreateDto dto,
        CancellationToken cancellationToken)
    {
        // TabelaMedidas nao implementa IAuditable: o carimbo vem do IClock.
        entidade.DataCriacao = _relogio.UtcNow;

        await ValidarLinhasAsync(dto.Linhas, cancellationToken);

        // Filhas anexadas a raiz ainda em Added: entram no MESMO SaveChanges do caso de uso.
        foreach (var linha in ConstruirLinhas(dto.Linhas))
            entidade.Linhas.Add(linha);
    }

    protected override async Task AntesDeAtualizarAsync(
        TabelaMedidas entidade,
        TabelaMedidasUpdateDto dto,
        CancellationToken cancellationToken)
    {
        await ValidarLinhasAsync(dto.Linhas, cancellationToken);

        var atuais = await Consulta.ListarAsync(
            _linhas.QueryTracked().Where(l => l.IdTabelaMedidas == entidade.Id),
            cancellationToken);

        // Remocao e insercao na mesma unidade de trabalho: nao existe instante em que a tabela
        // fica sem linha nenhuma para quem estiver lendo.
        _linhas.RemoverVarios(atuais);

        foreach (var linha in ConstruirLinhas(dto.Linhas))
        {
            linha.IdTabelaMedidas = entidade.Id;
            await _linhas.AdicionarAsync(linha, cancellationToken);
        }
    }

    /// <summary>
    /// A FK de produtos para tabelas_medidas e Restrict e vale inclusive para produto
    /// DESATIVADO — que continua apontando para a tabela mesmo sem aparecer em lugar nenhum.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(TabelaMedidas entidade, CancellationToken cancellationToken)
    {
        var emUso = await _tabelas.PossuiProdutosVinculadosAsync(entidade.Id, cancellationToken);

        BusinessValidationException.LancarSe(
            emUso,
            "Esta tabela de medidas esta vinculada a produtos (inclusive desativados). " +
            "Desvincule os produtos antes de remover.");

        var linhas = await Consulta.ListarAsync(
            _linhas.QueryTracked().Where(l => l.IdTabelaMedidas == entidade.Id),
            cancellationToken);

        _linhas.RemoverVarios(linhas);
    }

    private static IEnumerable<TabelaMedidasLinha> ConstruirLinhas(IReadOnlyList<TabelaMedidasLinhaDto> origem)
    {
        for (var posicao = 0; posicao < origem.Count; posicao++)
        {
            var linha = origem[posicao];

            yield return new TabelaMedidasLinha
            {
                IdTamanho = linha.IdTamanho,
                BustoCm = linha.BustoCm,
                CinturaCm = linha.CinturaCm,
                QuadrilCm = linha.QuadrilCm,
                ComprimentoCm = linha.ComprimentoCm,
                MangaCm = linha.MangaCm,
                // Ordem zerada em todas as linhas deixaria a exibicao por conta do banco; a
                // posicao no payload e a ordem que o admin viu na tela.
                Ordem = linha.Ordem > 0 ? linha.Ordem : posicao + 1
            };
        }
    }

    private async Task ValidarLinhasAsync(
        IReadOnlyList<TabelaMedidasLinhaDto> linhas,
        CancellationToken cancellationToken)
    {
        BusinessValidationException.LancarSe(
            linhas.Count == 0,
            "Informe ao menos uma linha de medidas: uma tabela vazia nao ajuda o cliente a escolher o tamanho.");

        var duplicado = linhas
            .GroupBy(l => l.IdTamanho)
            .FirstOrDefault(g => g.Count() > 1);

        BusinessValidationException.LancarSe(
            duplicado is not null,
            "Ha mais de uma linha para o mesmo tamanho na tabela de medidas.");

        var ids = linhas.Select(l => l.IdTamanho).Distinct().ToArray();
        var encontrados = await _tamanhos.ObterPorIdsAsync(ids, cancellationToken);

        var faltantes = ids.Except(encontrados.Select(t => t.Id)).ToArray();

        BusinessValidationException.LancarSe(
            faltantes.Length > 0,
            $"Tamanho(s) inexistente(s) na tabela de medidas: {string.Join(", ", faltantes)}.");
    }
}
