using Glorific.Application.Common;
using Glorific.Application.DTO.Estoque;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Domain.ReferenceData;

namespace Glorific.Application.Services;

/// <summary>
/// Movimenta estoque por SKU, sempre pelos UPDATEs condicionais atomicos do repositorio e
/// sempre gravando o ledger.
///
/// TRES DECISOES QUE VALE ENTENDER ANTES DE MEXER:
///
/// 1. NADA AQUI LE, DECIDE E ESCREVE. Toda mudanca de saldo passa por um UPDATE com WHERE que
///    ja carrega a regra ("... AND (quantidade - reservada) >= @q"), e "0 linhas afetadas" e a
///    resposta de negocio "nao tem". Ler o saldo, comparar em C# e gravar e exatamente como
///    dois checkouts simultaneos vendem a mesma peca duas vezes.
///
/// 2. QuantidadeAntes E DERIVADA DO DEPOIS, e nao lida antes do update. O saldo e reconsultado
///    DEPOIS da instrucao atomica (valor autoritativo) e o "antes" sai de
///    depois - delta. Ler o antes primeiro criaria uma janela em que outra transacao mexe no
///    meio e o ledger passa a mentir sobre o que aconteceu.
///
/// 3. AS PRIMITIVAS NAO COMMITAM. Reserva, liberacao e efetivacao compoem a transacao do
///    checkout e do webhook. Os casos de uso do painel (entrada, ajuste, parametros) commitam,
///    porque nao compoem nada maior. A divisao esta declarada em IEstoqueService.
/// </summary>
public class EstoqueService : IEstoqueService
{
    /// <summary>Movimentos aceitos como ENTRADA manual pelo painel.</summary>
    private static readonly MovimentoEstoqueKey[] EntradasPermitidas =
    [
        MovimentoEstoqueKeys.Reabastecimento,
        MovimentoEstoqueKeys.CadastroInicial,
        MovimentoEstoqueKeys.DevolucaoCliente
    ];

    /// <summary>Movimentos aceitos como AJUSTE/SAIDA manual pelo painel.</summary>
    private static readonly MovimentoEstoqueKey[] AjustesPermitidos =
    [
        MovimentoEstoqueKeys.AjusteInventario,
        MovimentoEstoqueKeys.PerdaAvaria,
        MovimentoEstoqueKeys.VendaManual
    ];

    private readonly IEstoqueRepository _estoques;
    private readonly IMovimentoEstoqueRepository _movimentos;
    private readonly IProdutoVariacaoRepository _variacoes;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConsultaAssincrona _consulta;
    private readonly IClock _relogio;

    public EstoqueService(
        IEstoqueRepository estoques,
        IMovimentoEstoqueRepository movimentos,
        IProdutoVariacaoRepository variacoes,
        IUsuarioRepository usuarios,
        IUnitOfWork unitOfWork,
        IConsultaAssincrona consulta,
        IClock relogio)
    {
        _estoques = estoques ?? throw new ArgumentNullException(nameof(estoques));
        _movimentos = movimentos ?? throw new ArgumentNullException(nameof(movimentos));
        _variacoes = variacoes ?? throw new ArgumentNullException(nameof(variacoes));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    // ------------------------------------------------------------------
    // Primitivas transacionais (nao commitam)
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<Resultado> ReservarAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return Resultado.Falha("Quantidade invalida para reserva de estoque.", "quantidade_invalida");

        var reservou = await _estoques.TentarReservarAsync(idVariacao, quantidade, cancellationToken);

        if (!reservou)
            return Resultado.Falha(
                await DescreverIndisponivelAsync(idVariacao, cancellationToken),
                "estoque_insuficiente");

        // Reserva e movimento de sinal ZERO: o fisico nao muda, so o comprometido. A quantidade
        // vai negativa porque e assim que o DISPONIVEL se move — sem isso o extrato nao explica
        // por que a peca sumiu da vitrine sem ter saido da prateleira.
        await GravarMovimentacaoAsync(
            idVariacao,
            MovimentoEstoqueKeys.ReservaCheckout,
            quantidadeSinalizada: -quantidade,
            deltaFisico: 0,
            idPedido,
            idUsuario,
            observacao: null,
            cancellationToken);

        return Resultado.Ok();
    }

    /// <inheritdoc />
    public async Task<Resultado> LiberarReservaAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        string? observacao = null,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return Resultado.Falha("Quantidade invalida para liberacao de reserva.", "quantidade_invalida");

        var liberou = await _estoques.LiberarReservaAsync(idVariacao, quantidade, cancellationToken);

        // False aqui NAO e erro do cliente: e reentrega de webhook de expiracao, ou compensacao
        // ja aplicada. O WHERE do repositorio impede reserva negativa; devolver o resultado
        // deixa o chamador registrar o fato sem abortar a transacao inteira.
        if (!liberou)
            return Resultado.Falha(
                "A reserva ja havia sido liberada para este item.",
                "reserva_inexistente");

        await GravarMovimentacaoAsync(
            idVariacao,
            MovimentoEstoqueKeys.LiberacaoReserva,
            quantidadeSinalizada: quantidade,
            deltaFisico: 0,
            idPedido,
            idUsuario,
            observacao,
            cancellationToken);

        return Resultado.Ok();
    }

    /// <inheritdoc />
    public async Task<Resultado> EfetivarVendaAsync(
        int idVariacao,
        int quantidade,
        int idPedido,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return Resultado.Falha("Quantidade invalida para efetivacao de venda.", "quantidade_invalida");

        var efetivou = await _estoques.TentarEfetivarVendaAsync(idVariacao, quantidade, cancellationToken);

        if (!efetivou)
            return Resultado.Falha(
                "Nao ha reserva correspondente para efetivar a venda deste item.",
                "reserva_inexistente");

        // Aqui o fisico cai de verdade: delta -quantidade. A reserva cai junto, na mesma
        // instrucao, e por isso o DISPONIVEL nao se move — ele ja tinha caido na reserva.
        await GravarMovimentacaoAsync(
            idVariacao,
            MovimentoEstoqueKeys.VendaSistema,
            quantidadeSinalizada: -quantidade,
            deltaFisico: -quantidade,
            idPedido,
            idUsuario: null,
            observacao: null,
            cancellationToken);

        return Resultado.Ok();
    }

    /// <inheritdoc />
    public async Task<Resultado> DevolverAoEstoqueAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        string? observacao = null,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            return Resultado.Falha("Quantidade invalida para devolucao ao estoque.", "quantidade_invalida");

        var entrou = await _estoques.RegistrarEntradaAsync(idVariacao, quantidade, cancellationToken);

        if (!entrou)
            return Resultado.Falha(
                "Este SKU nao possui registro de estoque para receber a devolucao.",
                "estoque_inexistente");

        await GravarMovimentacaoAsync(
            idVariacao,
            MovimentoEstoqueKeys.DevolucaoCliente,
            quantidadeSinalizada: quantidade,
            deltaFisico: quantidade,
            idPedido,
            idUsuario,
            observacao,
            cancellationToken);

        return Resultado.Ok();
    }

    // ------------------------------------------------------------------
    // Casos de uso do painel (commitam)
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<EstoqueVariacaoResponseDto>> RegistrarEntradaAsync(
        EstoqueEntradaDto dto,
        string? uuidUsuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Itens.Count == 0)
            throw new BusinessValidationException("Informe ao menos um item na entrada de estoque.");

        var chave = ResolverMovimento(dto.Movimento, EntradasPermitidas, MovimentoEstoqueKeys.Reabastecimento);
        var idUsuario = await ResolverUsuarioAsync(uuidUsuario, cancellationToken);

        // A nota inteira em UMA transacao: metade de uma nota lancada e pior que nenhuma, porque
        // ninguem sabe qual metade entrou.
        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var afetados = new List<int>();

        foreach (var item in dto.Itens)
        {
            if (item.Quantidade <= 0)
                throw new BusinessValidationException(
                    $"A quantidade da variacao {item.IdVariacao} deve ser maior que zero.");

            await GarantirRegistroDeEstoqueAsync(item.IdVariacao, cancellationToken);

            var entrou = await _estoques.RegistrarEntradaAsync(item.IdVariacao, item.Quantidade, cancellationToken);

            if (!entrou)
                throw new BusinessValidationException(
                    $"Nao foi possivel lancar a entrada da variacao {item.IdVariacao}.");

            await GravarMovimentacaoAsync(
                item.IdVariacao,
                chave,
                quantidadeSinalizada: item.Quantidade,
                deltaFisico: item.Quantidade,
                idPedido: null,
                idUsuario,
                dto.Observacao,
                cancellationToken);

            afetados.Add(item.IdVariacao);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return await MontarAsync(afetados, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EstoqueVariacaoResponseDto> AjustarAsync(
        EstoqueAjusteDto dto,
        string? uuidUsuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        BusinessValidationException.LancarSeVazio(dto.Observacao, "Descreva o motivo do ajuste de estoque.");

        var chave = ResolverMovimento(dto.Movimento, AjustesPermitidos, MovimentoEstoqueKeys.AjusteInventario);
        var idUsuario = await ResolverUsuarioAsync(uuidUsuario, cancellationToken);

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        await GarantirRegistroDeEstoqueAsync(dto.IdVariacao, cancellationToken);

        var atual = await _estoques.ObterPorVariacaoAsync(dto.IdVariacao, cancellationToken)
                    ?? throw new EntityNotFoundException("Estoque da variacao", dto.IdVariacao);

        var delta = dto.QuantidadeContada - atual.Quantidade;

        if (delta == 0)
            throw new BusinessValidationException(
                "A contagem informada e igual ao saldo atual: nao ha ajuste a registrar.");

        if (delta > 0)
        {
            var entrou = await _estoques.RegistrarEntradaAsync(dto.IdVariacao, delta, cancellationToken);

            if (!entrou)
                throw new BusinessValidationException("Nao foi possivel aplicar o ajuste positivo de estoque.");
        }
        else
        {
            // Baixa que respeita reserva alheia: o UPDATE exige saldo LIVRE. Ajustar por cima de
            // uma reserva derrubaria um pedido de outro cliente que ja foi pago.
            var baixou = await _estoques.TentarBaixarFisicoAsync(dto.IdVariacao, -delta, cancellationToken);

            if (!baixou)
                throw new BusinessValidationException(
                    $"Ajuste recusado: ha {atual.QuantidadeReservada} peca(s) reservada(s) em pedidos aguardando " +
                    "pagamento. Libere ou conclua esses pedidos antes de reduzir o saldo.");
        }

        await GravarMovimentacaoAsync(
            dto.IdVariacao,
            chave,
            quantidadeSinalizada: delta,
            deltaFisico: delta,
            idPedido: null,
            idUsuario,
            dto.Observacao,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return (await MontarAsync([dto.IdVariacao], cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<EstoqueVariacaoResponseDto> AtualizarParametrosAsync(
        int idVariacao,
        EstoqueParametrosUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await GarantirRegistroDeEstoqueAsync(idVariacao, cancellationToken);

        var estoque = await _consulta.PrimeiroOuPadraoAsync(
            _estoques.QueryTracked().Where(e => e.IdVariacao == idVariacao),
            cancellationToken)
            ?? throw new EntityNotFoundException("Estoque da variacao", idVariacao);

        estoque.QuantidadeMinima = dto.QuantidadeMinima;
        estoque.Localizacao = string.IsNullOrWhiteSpace(dto.Localizacao) ? null : dto.Localizacao.Trim();

        _estoques.Atualizar(estoque);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await MontarAsync([idVariacao], cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<EstoqueVariacaoResponseDto> ObterPorVariacaoAsync(
        int idVariacao,
        CancellationToken cancellationToken = default)
    {
        var montados = await MontarAsync([idVariacao], cancellationToken);

        return montados.Count > 0
            ? montados[0]
            : throw new EntityNotFoundException("Estoque da variacao", idVariacao);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EstoqueVariacaoResponseDto>> ObterAbaixoDoMinimoAsync(
        CancellationToken cancellationToken = default)
    {
        var abaixo = await _estoques.ObterAbaixoDoMinimoAsync(cancellationToken);

        // O repositorio ja traz variacao, produto, tamanho e cor carregados.
        return [.. abaixo.Select(Mapear)];
    }

    /// <inheritdoc />
    public async Task<PagedResult<MovimentacaoEstoqueResponseDto>> ListarMovimentacoesAsync(
        MovimentacaoEstoqueFiltro filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        filtro ??= new MovimentacaoEstoqueFiltro();
        requisicao ??= new PageRequest();

        var consulta = _movimentos.QueryMovimentacoes();

        if (filtro.IdVariacao is > 0)
            consulta = consulta.Where(m => m.IdVariacao == filtro.IdVariacao);

        if (filtro.IdPedido is > 0)
            consulta = consulta.Where(m => m.IdPedido == filtro.IdPedido);

        if (!string.IsNullOrWhiteSpace(filtro.Movimento))
        {
            var nome = filtro.Movimento.Trim();
            consulta = consulta.Where(m => m.Movimento.Nome == nome);
        }

        if (filtro.DeUtc is not null)
            consulta = consulta.Where(m => m.DataMovimentacao >= filtro.DeUtc);

        if (filtro.AteUtc is not null)
            consulta = consulta.Where(m => m.DataMovimentacao <= filtro.AteUtc);

        // COUNT antes do Skip/Take: Total e a contagem no banco, nunca Items.Count.
        var total = await _consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<MovimentacaoEstoqueResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        // Projecao ANONIMA no banco: o ledger tem colunas que a tela nao usa e navegacoes que
        // trariam produto inteiro por linha. Trazer so o necessario e o que mantem o extrato
        // barato depois de um ano de operacao.
        var pagina = consulta
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(m => new MovimentacaoEstoqueResponseDto
            {
                Id = m.Id,
                IdVariacao = m.IdVariacao,
                Sku = m.Variacao.Sku,
                NomeProduto = m.Variacao.Produto.Nome,
                Movimento = m.Movimento.Nome,
                Quantidade = m.Quantidade,
                QuantidadeAntes = m.QuantidadeAntes,
                QuantidadeDepois = m.QuantidadeDepois,
                IdPedido = m.IdPedido,
                Observacao = m.Observacao,
                DataMovimentacao = m.DataMovimentacao
            });

        var itens = await _consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<MovimentacaoEstoqueResponseDto>.Criar(itens, requisicao, total);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Grava a linha do ledger.
    ///
    /// O saldo e RECONSULTADO depois do UPDATE atomico e o "antes" e derivado dele. Ler o antes
    /// primeiro seria uma leitura fora da instrucao que decidiu — sob concorrencia o ledger
    /// passaria a registrar um estado que nunca existiu.
    ///
    /// QuantidadeAntes/Depois referem-se sempre ao estoque FISICO. Em reserva e liberacao o
    /// fisico nao muda (delta zero) e os dois numeros ficam iguais de proposito: o que se moveu
    /// foi o comprometido, e isso esta em Quantidade, com o sinal do efeito no disponivel.
    /// </summary>
    private async Task<MovimentacaoEstoque> GravarMovimentacaoAsync(
        int idVariacao,
        MovimentoEstoqueKey chave,
        int quantidadeSinalizada,
        int deltaFisico,
        int? idPedido,
        int? idUsuario,
        string? observacao,
        CancellationToken cancellationToken)
    {
        var idMovimento = await _movimentos.ObterIdPorChaveAsync(chave, cancellationToken);

        var estoque = await _estoques.ObterPorVariacaoAsync(idVariacao, cancellationToken)
                      ?? throw new EntityNotFoundException("Estoque da variacao", idVariacao);

        var depois = estoque.Quantidade;
        var antes = depois - deltaFisico;

        var movimentacao = new MovimentacaoEstoque
        {
            IdVariacao = idVariacao,
            IdMovimento = idMovimento,
            Quantidade = quantidadeSinalizada,
            QuantidadeAntes = antes,
            QuantidadeDepois = depois,
            IdPedido = idPedido,
            IdUsuario = idUsuario,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            DataMovimentacao = _relogio.UtcNow
        };

        await _movimentos.RegistrarMovimentacaoAsync(movimentacao, cancellationToken);

        return movimentacao;
    }

    /// <summary>
    /// Cria a linha de estoque na primeira entrada do SKU.
    ///
    /// A variacao nasce sem registro de estoque, e o primeiro lancamento e justamente o que o
    /// cria. Sem esta garantia o UPDATE condicional afetaria zero linhas e o painel diria
    /// "nao foi possivel lancar" para um cadastro perfeitamente valido.
    /// </summary>
    private async Task GarantirRegistroDeEstoqueAsync(int idVariacao, CancellationToken cancellationToken)
    {
        var existe = await _consulta.AlgumAsync(
            _estoques.Query().Where(e => e.IdVariacao == idVariacao),
            cancellationToken);

        if (existe)
            return;

        var variacaoExiste = await _consulta.AlgumAsync(
            _variacoes.Query().Where(v => v.Id == idVariacao),
            cancellationToken);

        if (!variacaoExiste)
            throw new EntityNotFoundException("Variacao", idVariacao);

        await _estoques.AdicionarAsync(
            new EstoqueVariacao
            {
                IdVariacao = idVariacao,
                Quantidade = 0,
                QuantidadeReservada = 0,
                QuantidadeMinima = 0,
                DataUltimaMovimentacao = _relogio.UtcNow
            },
            cancellationToken);

        // Salva JA: o UPDATE condicional do proximo passo vai direto ao banco e nao enxerga
        // uma linha que ainda esta so no ChangeTracker.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mensagem de esgotado com o rotulo que o cliente reconhece.
    ///
    /// "Tamanho M em Terracota esgotado" e acionavel; "estoque insuficiente para a variacao 412"
    /// obriga o cliente a adivinhar qual das tres linhas do carrinho falhou.
    /// </summary>
    private async Task<string> DescreverIndisponivelAsync(int idVariacao, CancellationToken cancellationToken)
    {
        var variacoes = await _variacoes.ObterParaCheckoutAsync([idVariacao], cancellationToken);
        var variacao = variacoes.FirstOrDefault();

        if (variacao is null)
            return "Um dos itens do carrinho nao esta mais disponivel.";

        return $"{Rotulo(variacao)} esgotado.";
    }

    private static string Rotulo(ProdutoVariacao variacao)
    {
        var produto = variacao.Produto?.Nome ?? variacao.Sku;
        var tamanho = variacao.Tamanho?.Codigo;
        var cor = variacao.Cor?.Nome;

        if (!string.IsNullOrWhiteSpace(tamanho) && !string.IsNullOrWhiteSpace(cor))
            return $"{produto} tamanho {tamanho} em {cor}";

        if (!string.IsNullOrWhiteSpace(tamanho))
            return $"{produto} tamanho {tamanho}";

        return produto;
    }

    /// <summary>
    /// Resolve a chave textual do movimento dentro do catalogo FECHADO permitido para a
    /// operacao. Aceitar texto livre aqui deixaria o admin inventar "Ajuste XPTO" e o relatorio
    /// de perdas parar de fechar sem ninguem perceber.
    /// </summary>
    private static MovimentoEstoqueKey ResolverMovimento(
        string? informado,
        IReadOnlyList<MovimentoEstoqueKey> permitidos,
        MovimentoEstoqueKey padrao)
    {
        if (string.IsNullOrWhiteSpace(informado))
            return padrao;

        var nome = informado.Trim();

        foreach (var chave in permitidos)
        {
            if (string.Equals(chave.Value, nome, StringComparison.OrdinalIgnoreCase))
                return chave;
        }

        throw new BusinessValidationException(
            $"Movimento de estoque invalido para esta operacao. Aceitos: {string.Join(", ", permitidos.Select(p => p.Value))}.");
    }

    /// <summary>
    /// Traduz o Uuid do token no id interno. Null quando a acao veio de worker ou webhook —
    /// movimentacao sem usuario e "sistema", e isso e legitimo no ledger.
    /// </summary>
    private async Task<int?> ResolverUsuarioAsync(string? uuidUsuario, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uuidUsuario))
            return null;

        var usuario = await _usuarios.ObterPorUuidAsync(uuidUsuario, cancellationToken);
        return usuario?.Id;
    }

    private async Task<IReadOnlyList<EstoqueVariacaoResponseDto>> MontarAsync(
        IReadOnlyCollection<int> idsVariacao,
        CancellationToken cancellationToken)
    {
        if (idsVariacao.Count == 0)
            return [];

        var ids = idsVariacao.Distinct().ToArray();

        var consulta = _estoques.Query()
            .Where(e => ids.Contains(e.IdVariacao))
            .Select(e => new EstoqueVariacaoResponseDto
            {
                IdVariacao = e.IdVariacao,
                Sku = e.Variacao.Sku,
                IdProduto = e.Variacao.IdProduto,
                NomeProduto = e.Variacao.Produto.Nome,
                Tamanho = e.Variacao.Tamanho.Codigo,
                Cor = e.Variacao.Cor.Nome,
                Quantidade = e.Quantidade,
                QuantidadeReservada = e.QuantidadeReservada,
                Disponivel = e.Quantidade - e.QuantidadeReservada,
                QuantidadeMinima = e.QuantidadeMinima,
                Localizacao = e.Localizacao,
                DataUltimaMovimentacao = e.DataUltimaMovimentacao,
                AbaixoDoMinimo = e.QuantidadeMinima > 0
                                 && (e.Quantidade - e.QuantidadeReservada) < e.QuantidadeMinima
            });

        return await _consulta.ListarAsync(consulta, cancellationToken);
    }

    /// <summary>Projecao em memoria, para a lista que ja veio com as navegacoes carregadas.</summary>
    private static EstoqueVariacaoResponseDto Mapear(EstoqueVariacao estoque) => new()
    {
        IdVariacao = estoque.IdVariacao,
        Sku = estoque.Variacao?.Sku ?? string.Empty,
        IdProduto = estoque.Variacao?.IdProduto ?? 0,
        NomeProduto = estoque.Variacao?.Produto?.Nome ?? string.Empty,
        Tamanho = estoque.Variacao?.Tamanho?.Codigo,
        Cor = estoque.Variacao?.Cor?.Nome,
        Quantidade = estoque.Quantidade,
        QuantidadeReservada = estoque.QuantidadeReservada,
        Disponivel = estoque.Disponivel,
        QuantidadeMinima = estoque.QuantidadeMinima,
        Localizacao = estoque.Localizacao,
        DataUltimaMovimentacao = estoque.DataUltimaMovimentacao,
        AbaixoDoMinimo = estoque.QuantidadeMinima > 0 && estoque.Disponivel < estoque.QuantidadeMinima
    };
}
