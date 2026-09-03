using System.Linq.Expressions;
using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// A ProdutoVariacao e o SKU REAL: o que tem estoque, o que tem peso e o que e vendido.
///
/// A REGRA CENTRAL deste servico: variacao NAO pode ser publicada sem peso e dimensoes. O
/// Melhor Envio exige products[].weight/width/height/length em POST /api/shipment/calculate —
/// sem eles nao existe cotacao de frete, e uma peca sem cotacao e uma peca que o cliente coloca
/// no carrinho e nao consegue comprar. O banco tem o CHECK como rede; aqui a recusa vira
/// mensagem que diz o que preencher.
/// </summary>
public class ProdutoVariacaoService
    : GenericService<ProdutoVariacao, ProdutoVariacaoCreateDto, ProdutoVariacaoUpdateDto, ProdutoVariacaoResponseDto>,
      IProdutoVariacaoService
{
    /// <summary>Teto do lote de grade: 20 tamanhos x 20 cores ja e um erro de digitacao.</summary>
    private const int MaximoCombinacoesPorLote = 200;

    private const int TamanhoMaximoSku = 60;

    private readonly IProdutoVariacaoRepository _variacoes;
    private readonly IProdutoRepository _produtos;
    private readonly ITamanhoRepository _tamanhos;
    private readonly ICorRepository _cores;
    private readonly IConsultaCatalogoSemFiltro _semFiltro;

    public ProdutoVariacaoService(
        IProdutoVariacaoRepository variacoes,
        IProdutoRepository produtos,
        ITamanhoRepository tamanhos,
        ICorRepository cores,
        IConsultaCatalogoSemFiltro semFiltro,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta)
        : base(variacoes, unitOfWork, mapper, consulta)
    {
        _variacoes = variacoes;
        _produtos = produtos;
        _tamanhos = tamanhos;
        _cores = cores;
        _semFiltro = semFiltro;
    }

    protected override string NomeEntidade => "Variacao de produto";

    // ------------------------------------------------------------------
    // Projecao
    // ------------------------------------------------------------------

    /// <summary>
    /// Uma unica definicao da resposta, usada nas duas formas: como Expression nas consultas que
    /// rodam no banco e compilada nos caminhos que ja trouxeram a entidade. Duplicar a montagem
    /// e como um dos dois lados fica para tras e passa a devolver campo vazio.
    /// </summary>
    private static readonly Expression<Func<ProdutoVariacao, ProdutoVariacaoResponseDto>> Projecao =
        variacao => new ProdutoVariacaoResponseDto
        {
            Id = variacao.Id,
            IdProduto = variacao.IdProduto,
            Sku = variacao.Sku,
            IdTamanho = variacao.IdTamanho,
            CodigoTamanho = variacao.Tamanho.Codigo,
            OrdemTamanho = variacao.Tamanho.Ordem,
            IdCor = variacao.IdCor,
            NomeCor = variacao.Cor.Nome,
            SlugCor = variacao.Cor.Slug,
            HexRgb = variacao.Cor.HexRgb,
            PrecoCentavos = variacao.PrecoCentavos,
            PrecoEfetivoCentavos = variacao.PrecoCentavos ?? variacao.Produto.PrecoBaseCentavos,
            CodigoBarras = variacao.CodigoBarras,
            PesoGramas = variacao.PesoGramas,
            AlturaCm = variacao.AlturaCm,
            LarguraCm = variacao.LarguraCm,
            ComprimentoCm = variacao.ComprimentoCm,
            Ativo = variacao.Ativo,
            QuantidadeEmEstoque = variacao.Estoque == null ? 0 : variacao.Estoque.Quantidade,
            QuantidadeReservada = variacao.Estoque == null ? 0 : variacao.Estoque.QuantidadeReservada,
            QuantidadeDisponivel = variacao.Estoque == null
                ? 0
                : variacao.Estoque.Quantidade - variacao.Estoque.QuantidadeReservada,
            QuantidadeMinima = variacao.Estoque == null ? 0 : variacao.Estoque.QuantidadeMinima
        };

    private static readonly Func<ProdutoVariacao, ProdutoVariacaoResponseDto> ProjecaoCompilada = Projecao.Compile();

    /// <summary>
    /// Exige Tamanho, Cor e Produto carregados — todos os caminhos que chegam aqui passam por
    /// repositorio que faz Include. Onde nao ha Include, a consulta projeta com <c>Projecao</c>.
    /// </summary>
    protected override ProdutoVariacaoResponseDto Mapear(ProdutoVariacao entidade) => ProjecaoCompilada(entidade);

    // ------------------------------------------------------------------
    // Leitura
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<ProdutoVariacaoResponseDto> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Sem filtro: o painel precisa abrir a variacao que ele mesmo desativou.
        var dto = await Consulta.PrimeiroOuPadraoAsync(
            _semFiltro.Variacoes().Where(v => v.Id == id).Select(Projecao),
            cancellationToken);

        return dto ?? throw new EntityNotFoundException(NomeEntidade, id);
    }

    /// <summary>
    /// A listagem generica herdada partiria de Repositorio.Query(), que nao carrega Tamanho,
    /// Cor nem Estoque — a resposta sairia sem tamanho, sem cor e com saldo zero. Aqui ela e
    /// redirecionada para a consulta projetada, que resolve tudo no banco.
    /// </summary>
    public override Task<PagedResult<ProdutoVariacaoResponseDto>> ListarAsync(
        PageRequest requisicao,
        CancellationToken cancellationToken = default) =>
        ListarAdminAsync(requisicao, null, null, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProdutoVariacaoResponseDto>> ObterPorProdutoAsync(
        int idProduto,
        bool incluirInativas = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = _semFiltro.Variacoes().Where(v => v.IdProduto == idProduto);

        if (!incluirInativas)
            consulta = consulta.Where(v => v.Ativo);

        // Ordem do seletor da vitrine: cor, depois tamanho por Ordem. Nunca alfabetica.
        var itens = await Consulta.ListarAsync(
            consulta
                .OrderBy(v => v.Cor.Ordem)
                .ThenBy(v => v.Cor.Nome)
                .ThenBy(v => v.Tamanho.Ordem)
                .ThenBy(v => v.Id)
                .Select(Projecao),
            cancellationToken);

        return itens;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ProdutoVariacaoResponseDto>> ListarAdminAsync(
        PageRequest requisicao,
        int? idProduto = null,
        string? busca = null,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = _semFiltro.Variacoes();

        if (idProduto is not null)
            consulta = consulta.Where(v => v.IdProduto == idProduto.Value);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLowerInvariant();

            consulta = consulta.Where(v =>
                v.Sku.ToLower().Contains(termo)
                || v.Produto.Nome.ToLower().Contains(termo));
        }

        // COUNT antes do Skip/Take: Total e a contagem no banco.
        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<ProdutoVariacaoResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta
            .OrderBy(v => v.IdProduto)
            .ThenBy(v => v.Cor.Ordem)
            .ThenBy(v => v.Tamanho.Ordem)
            .ThenBy(v => v.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(Projecao);

        var itens = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<ProdutoVariacaoResponseDto>.Criar(itens, requisicao, total);
    }

    // ------------------------------------------------------------------
    // Escrita
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<ProdutoVariacaoResponseDto> CriarAsync(
        ProdutoVariacaoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // ObterParaHistoricoAsync ignora o filtro de soft delete: montar a grade de um produto
        // ainda despublicado e o fluxo NORMAL do cadastro de moda, nao a excecao.
        var produto = await _produtos.ObterParaHistoricoAsync(dto.IdProduto, cancellationToken)
            ?? throw new EntityNotFoundException("Produto", dto.IdProduto);

        var tamanho = await _tamanhos.ObterPorIdAsync(dto.IdTamanho, cancellationToken)
            ?? throw new BusinessValidationException("O tamanho informado nao existe.");

        var cor = await _cores.ObterPorIdAsync(dto.IdCor, cancellationToken)
            ?? throw new BusinessValidationException("A cor informada nao existe.");

        GarantirDimensoesDePublicacao(
            dto.Ativo, dto.PesoGramas, dto.AlturaCm, dto.LarguraCm, dto.ComprimentoCm);

        // Inclui variacao DESATIVADA: ela continua ocupando a combinacao no indice unico.
        var combinacaoEmUso = await _variacoes.CombinacaoEmUsoAsync(
            produto.Id, dto.IdTamanho, dto.IdCor, null, cancellationToken);

        BusinessValidationException.LancarSe(
            combinacaoEmUso,
            $"Ja existe uma variacao de '{produto.Nome}' no tamanho {tamanho.Codigo} na cor {cor.Nome}.");

        var sku = await ResolverSkuAsync(dto.Sku, produto, tamanho, cor, null, cancellationToken);

        var variacao = new ProdutoVariacao
        {
            IdProduto = produto.Id,
            Sku = sku,
            IdTamanho = dto.IdTamanho,
            IdCor = dto.IdCor,
            PrecoCentavos = dto.PrecoCentavos,
            CodigoBarras = string.IsNullOrWhiteSpace(dto.CodigoBarras) ? null : dto.CodigoBarras.Trim(),
            PesoGramas = dto.PesoGramas,
            AlturaCm = dto.AlturaCm,
            LarguraCm = dto.LarguraCm,
            ComprimentoCm = dto.ComprimentoCm,
            Ativo = dto.Ativo,
            // A linha de estoque nasce JUNTO com o SKU. Sem ela a vitrine (que exige
            // Estoque != null) nunca mostraria a peca e o admin so descobriria no relatorio.
            Estoque = new EstoqueVariacao
            {
                Quantidade = dto.QuantidadeInicial,
                QuantidadeReservada = 0,
                QuantidadeMinima = dto.QuantidadeMinima
            }
        };

        await _variacoes.AdicionarAsync(variacao, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(variacao.Id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<ProdutoVariacaoResponseDto> AtualizarAsync(
        int id,
        ProdutoVariacaoUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var variacao = await _semFiltro.ObterVariacaoParaEdicaoAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        GarantirDimensoesDePublicacao(
            dto.Ativo, dto.PesoGramas, dto.AlturaCm, dto.LarguraCm, dto.ComprimentoCm);

        if (!string.IsNullOrWhiteSpace(dto.Sku))
        {
            var informado = dto.Sku.Trim().ToUpperInvariant();

            if (!string.Equals(informado, variacao.Sku, StringComparison.Ordinal))
            {
                var emUso = await _variacoes.SkuEmUsoAsync(informado, id, cancellationToken);

                BusinessValidationException.LancarSe(emUso, $"O SKU '{informado}' ja esta em uso.");

                variacao.Sku = informado;
            }
        }

        variacao.PrecoCentavos = dto.PrecoCentavos;
        variacao.CodigoBarras = string.IsNullOrWhiteSpace(dto.CodigoBarras) ? null : dto.CodigoBarras.Trim();
        variacao.PesoGramas = dto.PesoGramas;
        variacao.AlturaCm = dto.AlturaCm;
        variacao.LarguraCm = dto.LarguraCm;
        variacao.ComprimentoCm = dto.ComprimentoCm;
        variacao.Ativo = dto.Ativo;

        // A entidade veio rastreada: o ChangeTracker ja tem o delta. Quem salva e o caso de uso.
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Soft delete tambem aqui: o SKU aparece em pedido, etiqueta e recibo ja emitidos, e apagar
    /// a linha deixaria o historico do cliente sem tamanho e sem cor.
    /// </summary>
    public override async Task RemoverAsync(int id, CancellationToken cancellationToken = default)
    {
        var variacao = await _semFiltro.ObterVariacaoParaEdicaoAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        variacao.Ativo = false;

        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProdutoVariacaoResponseDto> AtivarAsync(int id, CancellationToken cancellationToken = default)
    {
        var variacao = await _semFiltro.ObterVariacaoParaEdicaoAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, id);

        // Reativar E publicar: a mesma regra de peso e dimensao vale aqui.
        GarantirDimensoesDePublicacao(
            true, variacao.PesoGramas, variacao.AlturaCm, variacao.LarguraCm, variacao.ComprimentoCm);

        variacao.Ativo = true;

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GradeGeradaDto> GerarGradeAsync(
        int idProduto,
        GerarGradeDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var produto = await _produtos.ObterParaHistoricoAsync(idProduto, cancellationToken)
            ?? throw new EntityNotFoundException("Produto", idProduto);

        var idsTamanhos = dto.IdsTamanhos.Distinct().ToArray();
        var idsCores = dto.IdsCores.Distinct().ToArray();

        BusinessValidationException.LancarSe(
            idsTamanhos.Length == 0 || idsCores.Length == 0,
            "Informe ao menos um tamanho e uma cor para gerar a grade.");

        BusinessValidationException.LancarSe(
            idsTamanhos.Length * idsCores.Length > MaximoCombinacoesPorLote,
            $"A grade solicitada tem mais de {MaximoCombinacoesPorLote} combinacoes. Gere em partes.");

        GarantirDimensoesDePublicacao(
            dto.Ativo, dto.PesoGramas, dto.AlturaCm, dto.LarguraCm, dto.ComprimentoCm);

        var tamanhos = await _tamanhos.ObterPorIdsAsync(idsTamanhos, cancellationToken);
        var cores = await _cores.ObterPorIdsAsync(idsCores, cancellationToken);

        GarantirTodosEncontrados(idsTamanhos, [.. tamanhos.Select(t => t.Id)], "tamanho");
        GarantirTodosEncontrados(idsCores, [.. cores.Select(c => c.Id)], "cor");

        // Combinacoes ja existentes, INCLUSIVE as desativadas: elas ocupam o indice unico
        // (produto, tamanho, cor) e recriar estouraria com violacao crua.
        var existentes = await Consulta.ListarAsync(
            _semFiltro.Variacoes()
                .Where(v => v.IdProduto == idProduto)
                .Select(v => new { v.IdTamanho, v.IdCor }),
            cancellationToken);

        var ocupadas = existentes
            .Select(x => (x.IdTamanho, x.IdCor))
            .ToHashSet();

        var prefixo = string.IsNullOrWhiteSpace(dto.PrefixoSku) ? produto.SkuBase : dto.PrefixoSku.Trim();

        var novas = new List<ProdutoVariacao>();

        // Ordem determinista (cor, depois tamanho) para o SKU gerado ser reprodutivel entre
        // execucoes — o admin confere a grade lendo a lista, e ordem instavel confunde.
        foreach (var cor in cores.OrderBy(c => c.Ordem).ThenBy(c => c.Nome))
        {
            foreach (var tamanho in tamanhos.OrderBy(t => t.Ordem).ThenBy(t => t.Codigo))
            {
                if (ocupadas.Contains((tamanho.Id, cor.Id)))
                    continue;

                var sku = await ResolverSkuAsync(null, produto, tamanho, cor, null, cancellationToken, prefixo);

                novas.Add(new ProdutoVariacao
                {
                    IdProduto = produto.Id,
                    Sku = sku,
                    IdTamanho = tamanho.Id,
                    IdCor = cor.Id,
                    PrecoCentavos = dto.PrecoCentavos,
                    PesoGramas = dto.PesoGramas,
                    AlturaCm = dto.AlturaCm,
                    LarguraCm = dto.LarguraCm,
                    ComprimentoCm = dto.ComprimentoCm,
                    Ativo = dto.Ativo,
                    Estoque = new EstoqueVariacao
                    {
                        Quantidade = dto.QuantidadeInicial,
                        QuantidadeReservada = 0,
                        QuantidadeMinima = dto.QuantidadeMinima
                    }
                });
            }
        }

        if (novas.Count > 0)
        {
            await _variacoes.AdicionarVariosAsync(novas, cancellationToken);

            // Um unico SaveChanges para o lote inteiro: e o que torna a geracao de grade viavel.
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        var grade = await ObterPorProdutoAsync(idProduto, incluirInativas: true, cancellationToken);

        return new GradeGeradaDto
        {
            IdProduto = idProduto,
            Criadas = novas.Count,
            JaExistiam = (idsTamanhos.Length * idsCores.Length) - novas.Count,
            Variacoes = grade
        };
    }

    // ------------------------------------------------------------------
    // Regras
    // ------------------------------------------------------------------

    /// <summary>
    /// A REGRA: variacao nao pode ser publicada sem peso e dimensoes.
    ///
    /// Nao e preciosismo de cadastro — o Melhor Envio exige weight/width/height/length em
    /// POST /api/shipment/calculate. Publicar sem esses dados coloca no ar uma peca que o
    /// cliente adiciona ao carrinho e para na tela de frete, sem entender por que.
    /// O erro sai por CAMPO para a tela destacar exatamente o que falta.
    /// </summary>
    private static void GarantirDimensoesDePublicacao(
        bool ativo,
        int pesoGramas,
        decimal alturaCm,
        decimal larguraCm,
        decimal comprimentoCm)
    {
        var faltando = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (pesoGramas <= 0)
            faltando[nameof(ProdutoVariacaoCreateDto.PesoGramas)] = ["Informe o peso em gramas (maior que zero)."];

        if (alturaCm <= 0)
            faltando[nameof(ProdutoVariacaoCreateDto.AlturaCm)] = ["Informe a altura em cm (maior que zero)."];

        if (larguraCm <= 0)
            faltando[nameof(ProdutoVariacaoCreateDto.LarguraCm)] = ["Informe a largura em cm (maior que zero)."];

        if (comprimentoCm <= 0)
            faltando[nameof(ProdutoVariacaoCreateDto.ComprimentoCm)] = ["Informe o comprimento em cm (maior que zero)."];

        if (faltando.Count == 0)
            return;

        // A mensagem muda conforme a intencao: publicar sem medida e um bloqueio de negocio;
        // salvar rascunho sem medida esbarra no CHECK do banco, e o texto precisa dizer isso.
        var mensagem = ativo
            ? "Nao e possivel publicar esta variacao sem peso e dimensoes: sem esses dados o frete " +
              "nao pode ser calculado e a peca nao pode ser vendida."
            : "Peso e dimensoes sao obrigatorios em toda variacao, mesmo desativada.";

        throw new BusinessValidationException(mensagem, faltando);
    }

    private static void GarantirTodosEncontrados(
        IReadOnlyCollection<int> solicitados,
        IReadOnlyCollection<int> encontrados,
        string rotulo)
    {
        var faltantes = solicitados.Except(encontrados).ToArray();

        BusinessValidationException.LancarSe(
            faltantes.Length > 0,
            $"Nao foi possivel gerar a grade: {rotulo}(s) inexistente(s) — {string.Join(", ", faltantes)}.");
    }

    /// <summary>
    /// SKU informado a mao e respeitado e conferido; SKU vazio e derivado de
    /// SKU base + tamanho + cor, com sufixo numerico quando a derivacao colide.
    ///
    /// A diferenca de tratamento e proposital: sufixar em silencio um SKU que o admin digitou
    /// gravaria no banco um codigo diferente do que ele colou da planilha do fornecedor.
    /// </summary>
    private async Task<string> ResolverSkuAsync(
        string? informado,
        Produto produto,
        Tamanho tamanho,
        Cor cor,
        int? idIgnorar,
        CancellationToken cancellationToken,
        string? prefixo = null)
    {
        if (!string.IsNullOrWhiteSpace(informado))
        {
            var manual = informado.Trim().ToUpperInvariant();

            var emUso = await _variacoes.SkuEmUsoAsync(manual, idIgnorar, cancellationToken);

            BusinessValidationException.LancarSe(emUso, $"O SKU '{manual}' ja esta em uso.");

            return manual;
        }

        var raiz = SlugHelper
            .Gerar($"{prefixo ?? produto.SkuBase} {tamanho.Codigo} {cor.Slug}")
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(raiz))
            raiz = SlugHelper.Gerar(produto.SkuBase).ToUpperInvariant();

        // Reserva espaco para o sufixo antes de truncar: cortar em 60 e depois concatenar "-2"
        // estouraria o limite da coluna.
        if (raiz.Length > TamanhoMaximoSku - 5)
            raiz = raiz[..(TamanhoMaximoSku - 5)];

        for (var sufixo = 1; sufixo <= 200; sufixo++)
        {
            var candidato = SlugHelper.ComSufixo(raiz, sufixo);

            if (!await _variacoes.SkuEmUsoAsync(candidato, idIgnorar, cancellationToken))
                return candidato;
        }

        throw new BusinessValidationException(
            $"Nao foi possivel gerar um SKU unico para {tamanho.Codigo}/{cor.Nome}. Informe o SKU manualmente.");
    }
}
