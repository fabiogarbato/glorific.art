using Glorific.Application.Common;
using Glorific.Application.DTO.Social;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Social;
using Glorific.Domain.Enums;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Avaliacao de produto.
///
/// Tres regras nao negociaveis moram aqui:
///
/// 1. So avalia quem comprou. A prova e um PedidoItem daquele produto num pedido do proprio
///    usuario — e o mesmo vinculo que sustenta o selo de compra verificada. Sem isso, a pagina de
///    produto vira mural aberto e a nota media deixa de significar qualquer coisa.
///
/// 2. Uma avaliacao por produto por usuario, em QUALQUER status. Quem teve a review rejeitada nao
///    reenvia a mesma por baixo.
///
/// 3. Nasce Pendente. Loja crista com comentario publicado sem moderacao e risco de marca, nao de
///    produto: o custo de moderar e baixo, o de despublicar depois de viralizar e alto.
///
/// A moderacao recalcula NotaMedia e TotalAvaliacoes do produto DEPOIS do SaveChanges, porque
/// RecalcularNotasAsync executa um UPDATE direto no banco e precisa enxergar o status ja gravado.
/// </summary>
public class AvaliacaoService : IAvaliacaoService
{
    /// <summary>Comprou de verdade: pagamento confirmado. Pedido aguardando pagamento nao vale.</summary>
    private static readonly StatusPedido[] StatusQueComprovamCompra =
    [
        StatusPedido.Pago,
        StatusPedido.EmSeparacao,
        StatusPedido.Enviado,
        StatusPedido.Entregue
    ];

    private const int MaximoMidiasPorAvaliacao = 5;

    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IProdutoRepository _produtos;
    private readonly IPedidoRepository _pedidos;
    private readonly IMidiaRepository _midias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IConsultaAssincrona _consulta;
    private readonly IClock _relogio;

    public AvaliacaoService(
        IAvaliacaoRepository avaliacoes,
        IProdutoRepository produtos,
        IPedidoRepository pedidos,
        IMidiaRepository midias,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
    {
        _avaliacoes = avaliacoes ?? throw new ArgumentNullException(nameof(avaliacoes));
        _produtos = produtos ?? throw new ArgumentNullException(nameof(produtos));
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _midias = midias ?? throw new ArgumentNullException(nameof(midias));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    // ------------------------------------------------------------------
    // Vitrine
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<AvaliacaoResponseDto>> ListarDoProdutoAsync(
        int idProduto,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        // A consulta do repositorio ja carrega o filtro de aprovadas, o autor e as midias. O
        // filtro fica la, e nao aqui, porque esquecer o Where uma unica vez publica a fila de
        // moderacao inteira na pagina do produto.
        var consulta = _avaliacoes.QueryAprovadasDoProduto(idProduto);

        var total = await _consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<AvaliacaoResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta.Skip(requisicao.Skip).Take(requisicao.Take);

        var avaliacoes = await _consulta.ListarAsync(pagina, cancellationToken);

        var itens = avaliacoes.Select(avaliacao => _mapper.Map<AvaliacaoResponseDto>(avaliacao)).ToArray();

        return PagedResult<AvaliacaoResponseDto>.Criar(itens, requisicao, total);
    }

    /// <inheritdoc />
    public async Task<AvaliacaoResumoDto> ObterResumoDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        var (media, total) = await _avaliacoes.ObterResumoAsync(idProduto, cancellationToken);

        var aprovadas = _avaliacoes.Query()
            .Where(avaliacao => avaliacao.IdProduto == idProduto
                                && avaliacao.Status == StatusAvaliacao.Aprovada);

        // Agrupado no banco: no maximo cinco linhas voltam, nunca a tabela de avaliacoes.
        // Recomendacao e somada dentro do mesmo GROUP BY para nao pagar uma segunda ida.
        var porNota = await _consulta.ListarAsync(
            aprovadas
                .GroupBy(avaliacao => avaliacao.Nota)
                .Select(grupo => new
                {
                    Nota = grupo.Key,
                    Quantidade = grupo.Count(),
                    Recomendam = grupo.Sum(avaliacao => avaliacao.Recomenda == true ? 1 : 0),
                    Responderam = grupo.Sum(avaliacao => avaliacao.Recomenda != null ? 1 : 0)
                }),
            cancellationToken);

        var porCaimento = await _consulta.ListarAsync(
            aprovadas
                .GroupBy(avaliacao => avaliacao.Caimento)
                .Select(grupo => new { Caimento = grupo.Key, Quantidade = grupo.Count() }),
            cancellationToken);

        var distribuicao = new Dictionary<int, int>();
        for (var nota = 1; nota <= 5; nota++)
            distribuicao[nota] = porNota.FirstOrDefault(linha => linha.Nota == nota)?.Quantidade ?? 0;

        var responderam = porNota.Sum(linha => linha.Responderam);
        var recomendam = porNota.Sum(linha => linha.Recomendam);

        var caimentoDominante = porCaimento
            .Where(linha => linha.Caimento is not null)
            .OrderByDescending(linha => linha.Quantidade)
            .FirstOrDefault();

        return new AvaliacaoResumoDto
        {
            IdProduto = idProduto,
            NotaMedia = media,
            TotalAvaliacoes = total,
            DistribuicaoPorNota = distribuicao,
            // Percentual inteiro, arredondado: "89% recomendam" e a leitura que a pagina faz.
            PercentualRecomenda = responderam == 0
                ? (int?)null
                : (int?)Math.Round(recomendam * 100m / responderam, MidpointRounding.AwayFromZero),
            CaimentoPredominante = caimentoDominante?.Caimento,
            TotalRespostasCaimento = porCaimento
                .Where(linha => linha.Caimento is not null)
                .Sum(linha => linha.Quantidade)
        };
    }

    // ------------------------------------------------------------------
    // Cliente
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<AvaliacaoResponseDto> CriarAsync(
        int idUsuario,
        AvaliacaoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _produtos.ExisteAsync(dto.IdProduto, cancellationToken))
            throw new EntityNotFoundException("Produto", dto.IdProduto);

        if (await _avaliacoes.ExisteDoUsuarioAsync(dto.IdProduto, idUsuario, cancellationToken))
            throw new BusinessValidationException("Voce ja avaliou este produto.");

        var idPedidoItem = await ResolverCompraAsync(idUsuario, dto, cancellationToken);

        if (dto.IdsMidia.Count > MaximoMidiasPorAvaliacao)
            throw new BusinessValidationException(
                $"Envie no maximo {MaximoMidiasPorAvaliacao} fotos por avaliacao.");

        var avaliacao = new Avaliacao
        {
            IdProduto = dto.IdProduto,
            IdUsuario = idUsuario,
            IdPedidoItem = idPedidoItem,
            Nota = dto.Nota,
            Titulo = Limpar(dto.Titulo),
            Comentario = Limpar(dto.Comentario),
            TamanhoComprado = Limpar(dto.TamanhoComprado),
            AlturaClienteCm = dto.AlturaClienteCm,
            PesoClienteKg = dto.PesoClienteKg,
            Caimento = dto.Caimento,
            Recomenda = dto.Recomenda,

            // Moderacao previa: nasce pendente independentemente de quem escreveu.
            Status = StatusAvaliacao.Pendente,

            // Avaliacao nao e IAuditable, entao o carimbo nao vem do DbContext. IClock, nunca
            // DateTime.UtcNow direto: sem ele nao ha como testar ordenacao por data.
            DataCriacao = _relogio.UtcNow
        };

        await VincularMidiasAsync(avaliacao, dto.IdsMidia, cancellationToken);

        await _avaliacoes.AdicionarAsync(avaliacao, cancellationToken);

        // Quem salva e o caso de uso. As midias entram na mesma unidade de trabalho por estarem
        // penduradas na navegacao: uma avaliacao sem as fotos que a pessoa anexou seria pior que
        // nenhuma avaliacao.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AvaliacaoResponseDto>(avaliacao);
    }

    // ------------------------------------------------------------------
    // Moderacao
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<AvaliacaoAdminResponseDto>> ListarParaModeracaoAsync(
        StatusAvaliacao? status,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var alvo = status ?? StatusAvaliacao.Pendente;

        var consulta = _avaliacoes.Query().Where(avaliacao => avaliacao.Status == alvo);

        var total = await _consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<AvaliacaoAdminResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        // Fila de moderacao e FIFO: a review mais antiga e a que esta ha mais tempo invisivel para
        // quem escreveu. Ordenacao deterministica com desempate por Id, senao a linha da pagina 1
        // reaparece na pagina 2.
        var pagina = consulta
            .OrderBy(avaliacao => avaliacao.DataCriacao)
            .ThenBy(avaliacao => avaliacao.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take);

        var itens = await _consulta.ListarAsync(ProjetarAdmin(pagina), cancellationToken);

        return PagedResult<AvaliacaoAdminResponseDto>.Criar(itens, requisicao, total);
    }

    /// <inheritdoc />
    public Task<AvaliacaoAdminResponseDto> AprovarAsync(
        int idAvaliacao,
        int idModerador,
        CancellationToken cancellationToken = default) =>
        ModerarAsync(idAvaliacao, idModerador, StatusAvaliacao.Aprovada, null, cancellationToken);

    /// <inheritdoc />
    public Task<AvaliacaoAdminResponseDto> RejeitarAsync(
        int idAvaliacao,
        int idModerador,
        string motivo,
        CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(motivo, "Informe o motivo da rejeicao.");

        return ModerarAsync(idAvaliacao, idModerador, StatusAvaliacao.Rejeitada, motivo.Trim(), cancellationToken);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private async Task<AvaliacaoAdminResponseDto> ModerarAsync(
        int idAvaliacao,
        int idModerador,
        StatusAvaliacao novoStatus,
        string? motivo,
        CancellationToken cancellationToken)
    {
        var avaliacao = await _avaliacoes.ObterParaEdicaoAsync(idAvaliacao, cancellationToken)
            ?? throw new EntityNotFoundException("Avaliacao", idAvaliacao);

        if (avaliacao.Status == novoStatus)
            throw new BusinessValidationException("Esta avaliacao ja esta neste status.");

        avaliacao.Status = novoStatus;
        avaliacao.MotivoRejeicao = novoStatus == StatusAvaliacao.Rejeitada ? motivo : null;
        avaliacao.ModeradaPor = idModerador;
        avaliacao.ModeradaEm = _relogio.UtcNow;

        _avaliacoes.Atualizar(avaliacao);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // DEPOIS do SaveChanges, e nao antes: RecalcularNotasAsync roda um UPDATE direto no banco
        // e agrega em cima do que estiver commitado. Chamar antes recalcularia com o status velho.
        await _produtos.RecalcularNotasAsync(avaliacao.IdProduto, cancellationToken);

        return await ObterAdminAsync(idAvaliacao, cancellationToken);
    }

    private async Task<AvaliacaoAdminResponseDto> ObterAdminAsync(
        int idAvaliacao,
        CancellationToken cancellationToken)
    {
        var consulta = ProjetarAdmin(_avaliacoes.Query().Where(avaliacao => avaliacao.Id == idAvaliacao));

        return await _consulta.PrimeiroOuPadraoAsync(consulta, cancellationToken)
            ?? throw new EntityNotFoundException("Avaliacao", idAvaliacao);
    }

    /// <summary>
    /// Projecao administrativa feita NO BANCO. Nao usa as consultas com Include do repositorio de
    /// proposito: o painel precisa de nome do produto e e-mail do autor, e trazer os agregados
    /// inteiros para depois descartar quase tudo custa caro numa fila de moderacao grande.
    ///
    /// A guarda de null em Produto existe porque produto tem filtro global de soft delete: a peca
    /// desativada faria a navegacao vir nula e a linha da fila sumir sem explicacao.
    /// </summary>
    private static IQueryable<AvaliacaoAdminResponseDto> ProjetarAdmin(IQueryable<Avaliacao> consulta) =>
        consulta.Select(avaliacao => new AvaliacaoAdminResponseDto
        {
            Id = avaliacao.Id,
            IdProduto = avaliacao.IdProduto,
            NomeProduto = avaliacao.Produto == null ? string.Empty : avaliacao.Produto.Nome,
            IdUsuario = avaliacao.IdUsuario,
            NomeUsuario = avaliacao.Usuario == null ? null : avaliacao.Usuario.NomeCompleto,
            EmailUsuario = avaliacao.Usuario == null ? null : avaliacao.Usuario.Email,
            CompraVerificada = avaliacao.IdPedidoItem != null,
            Nota = avaliacao.Nota,
            Titulo = avaliacao.Titulo,
            Comentario = avaliacao.Comentario,
            TamanhoComprado = avaliacao.TamanhoComprado,
            AlturaClienteCm = avaliacao.AlturaClienteCm,
            PesoClienteKg = avaliacao.PesoClienteKg,
            Caimento = avaliacao.Caimento,
            Recomenda = avaliacao.Recomenda,
            Status = avaliacao.Status,
            MotivoRejeicao = avaliacao.MotivoRejeicao,
            ModeradaPor = avaliacao.ModeradaPor,
            ModeradaEm = avaliacao.ModeradaEm,
            DataCriacao = avaliacao.DataCriacao,
            Midias = avaliacao.Midias
                .OrderBy(midia => midia.Ordem)
                .Select(midia => new AvaliacaoMidiaResponseDto
                {
                    Id = midia.Id,
                    Url = midia.Midia.Url,
                    AltText = midia.Midia.AltText,
                    Ordem = midia.Ordem
                })
                .ToList()
        });

    /// <summary>
    /// Encontra a compra que autoriza a avaliacao.
    ///
    /// Quando o front informa o item, a checagem e direta e passa pelo repositorio, que ja ignora
    /// o filtro de soft delete: o direito de avaliar nao morre porque a peca saiu do catalogo.
    /// Quando nao informa, procuramos a compra mais recente daquele produto em pedido do proprio
    /// usuario com pagamento confirmado. Nao achar nada e 400, nao 403: o usuario esta autenticado,
    /// o que falta e o pre-requisito de negocio.
    /// </summary>
    private async Task<int?> ResolverCompraAsync(
        int idUsuario,
        AvaliacaoCreateDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.IdPedidoItem is { } informado)
        {
            var pertence = await _avaliacoes.ItemPertenceAoUsuarioAsync(
                informado, idUsuario, dto.IdProduto, cancellationToken);

            if (!pertence)
                throw new BusinessValidationException(
                    "Somente quem comprou esta peca pode avalia-la.");

            return informado;
        }

        var encontrado = await _consulta.PrimeiroOuPadraoAsync(
            _pedidos.Query()
                .Where(pedido => pedido.IdUsuario == idUsuario
                                 && StatusQueComprovamCompra.Contains(pedido.Status))
                .SelectMany(pedido => pedido.Itens)
                .Where(item => item.IdProduto == dto.IdProduto)
                .OrderByDescending(item => item.Id)
                .Select(item => (int?)item.Id),
            cancellationToken);

        if (encontrado is null)
            throw new BusinessValidationException(
                "Somente quem comprou esta peca pode avalia-la.");

        return encontrado;
    }

    /// <summary>
    /// Pendura as fotos na navegacao antes do Add, para tudo entrar num SaveChanges so.
    /// As midias sao validadas contra a tabela: id inventado no corpo da requisicao viraria FK
    /// quebrada na hora do commit, com erro de banco cru chegando na tela.
    /// </summary>
    private async Task VincularMidiasAsync(
        Avaliacao avaliacao,
        IReadOnlyList<int> idsMidia,
        CancellationToken cancellationToken)
    {
        if (idsMidia.Count == 0)
            return;

        var distintos = idsMidia.Distinct().ToArray();

        var existentes = await _midias.ObterPorIdsAsync(distintos, cancellationToken);

        if (existentes.Count != distintos.Length)
            throw new BusinessValidationException("Uma das fotos enviadas nao foi encontrada.");

        var ordem = 0;

        foreach (var idMidia in distintos)
        {
            avaliacao.Midias.Add(new AvaliacaoMidia
            {
                IdMidia = idMidia,
                Ordem = ordem++
            });
        }
    }

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
