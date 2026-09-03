using System.Globalization;
using Glorific.Application.DTO.Carrinho;
using Glorific.Application.Exceptions;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;

// A pasta Carrinho e ao mesmo tempo namespace de entidade e namespace de DTO. Sem os alias o
// compilador nao sabe se "Carrinho" e o tipo ou o namespace.
using CarrinhoEntity = Glorific.Domain.Entities.Carrinho.Carrinho;
using CarrinhoItemEntity = Glorific.Domain.Entities.Carrinho.CarrinhoItem;

namespace Glorific.Application.Services;

/// <summary>
/// Carrinho server-side: criacao sob demanda, itens, cupom e merge no login.
///
/// DECISOES QUE NAO SAO OBVIAS:
///
/// 1. GET NAO CRIA CARRINHO. Um visitante sem carrinho recebe um DTO vazio e nada e gravado.
///    Criar linha em toda leitura encheria a tabela com trafego de robo, e cada linha ocuparia
///    o slot do indice unico parcial de carrinho aberto por sessao.
///
/// 2. TODA MUTACAO DE LINHA REFRESCA O PrecoUnitarioSnapshot. O snapshot existe para avisar
///    "o preco deste item mudou" enquanto o carrinho ficou parado. Quando o cliente MEXE na
///    linha, ele esta olhando o preco atual na tela — congelar o preco velho ali faria o
///    checkout devolver 409 sem que exista qualquer acao no site capaz de resolver.
///
/// 3. O CUPOM AQUI E PREVIA. Aplicar grava o vinculo e calcula o desconto para exibicao, mas
///    NAO consome uso (isso e UPDATE condicional dentro da transacao do checkout) e nao
///    valida restricao de colecao — que exige a tabela de juncao e e reconferida no checkout,
///    que e a autoridade sobre o valor cobrado.
///
/// 4. DISPONIBILIDADE E INFORMATIVA. O carrinho nunca reserva. As flags Indisponivel e
///    QuantidadeAcimaDoDisponivel existem para a tela avisar antes, nao para garantir nada.
/// </summary>
public class CarrinhoService : ICarrinhoService
{
    /// <summary>Teto por linha, espelhando o Range dos DTOs de entrada.</summary>
    private const int MaximoPorLinha = 20;

    /// <summary>
    /// Validade do carrinho. Renovada a cada mutacao — carrinho mexido ontem nao e abandonado.
    /// E o prazo que o worker de abandono usa para expirar e liberar o slot do indice parcial.
    /// </summary>
    private const int DiasValidade = 30;

    private readonly ICarrinhoRepository _carrinhos;
    private readonly IProdutoVariacaoRepository _variacoes;
    private readonly ICupomRepository _cupons;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;

    public CarrinhoService(
        ICarrinhoRepository carrinhos,
        IProdutoVariacaoRepository variacoes,
        ICupomRepository cupons,
        IUsuarioRepository usuarios,
        IUnitOfWork unitOfWork,
        IClock relogio)
    {
        _carrinhos = carrinhos ?? throw new ArgumentNullException(nameof(carrinhos));
        _variacoes = variacoes ?? throw new ArgumentNullException(nameof(variacoes));
        _cupons = cupons ?? throw new ArgumentNullException(nameof(cupons));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    // ------------------------------------------------------------------
    // Leitura
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> ObterAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        var (idUsuario, carrinho) = await LocalizarAsync(identidade, cancellationToken);

        return carrinho is null
            ? Vazio()
            : await MontarAsync(carrinho, idUsuario, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Itens
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> AdicionarItemAsync(
        IdentidadeCarrinho identidade,
        CarrinhoItemCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Quantidade <= 0)
            throw new BusinessValidationException("Informe uma quantidade maior que zero.");

        var (idUsuario, carrinho) = await LocalizarOuCriarAsync(identidade, cancellationToken);

        // ObterParaCheckoutAsync mantem o filtro de soft delete ligado: variacao desativada
        // simplesmente nao volta, e o "sumiu da lista" e o sinal para recusar a inclusao.
        var variacao = (await _variacoes.ObterParaCheckoutAsync([dto.IdVariacao], cancellationToken))
            .FirstOrDefault()
            ?? throw new BusinessValidationException("Este item nao esta mais disponivel na loja.");

        var itemExistente = await _carrinhos.ObterItemAsync(carrinho.Id, dto.IdVariacao, cancellationToken);
        var quantidadeAtual = itemExistente?.Quantidade ?? 0;
        var quantidadeFinal = quantidadeAtual + dto.Quantidade;

        if (quantidadeFinal > MaximoPorLinha)
            throw new BusinessValidationException(
                $"Limite de {MaximoPorLinha} unidades por item. Para quantidades maiores, fale com a loja.");

        var disponivel = Disponivel(variacao);

        if (disponivel <= 0)
            throw new BusinessValidationException($"{Rotulo(variacao)} esgotado.");

        if (quantidadeFinal > disponivel)
            throw new BusinessValidationException(
                $"Restam apenas {disponivel} unidade(s) de {Rotulo(variacao)}.");

        var precoAtual = variacao.PrecoEfetivoCentavos;

        if (itemExistente is null)
        {
            await _carrinhos.AdicionarItemAsync(
                new CarrinhoItemEntity
                {
                    IdCarrinho = carrinho.Id,
                    IdVariacao = dto.IdVariacao,
                    Quantidade = quantidadeFinal,
                    PrecoUnitarioSnapshotCentavos = precoAtual,
                    DataAdicao = _relogio.UtcNow
                },
                cancellationToken);
        }
        else
        {
            itemExistente.Quantidade = quantidadeFinal;

            // Ver decisao 2 no cabecalho da classe: o cliente acabou de agir sobre esta linha
            // olhando o preco atual, entao o snapshot passa a ser esse.
            itemExistente.PrecoUnitarioSnapshotCentavos = precoAtual;
        }

        await RenovarValidadeAsync(carrinho.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> AlterarQuantidadeAsync(
        IdentidadeCarrinho identidade,
        int idItem,
        CarrinhoItemUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Quantidade <= 0)
            return await RemoverItemAsync(identidade, idItem, cancellationToken);

        var (idUsuario, carrinho) = await LocalizarOuFalharAsync(identidade, cancellationToken);

        var linha = LocalizarLinha(carrinho, idItem);

        var variacao = (await _variacoes.ObterParaCheckoutAsync([linha.IdVariacao], cancellationToken))
            .FirstOrDefault()
            ?? throw new BusinessValidationException("Este item nao esta mais disponivel na loja.");

        if (dto.Quantidade > MaximoPorLinha)
            throw new BusinessValidationException(
                $"Limite de {MaximoPorLinha} unidades por item.");

        var disponivel = Disponivel(variacao);

        if (dto.Quantidade > disponivel)
            throw new BusinessValidationException(
                disponivel <= 0
                    ? $"{Rotulo(variacao)} esgotado."
                    : $"Restam apenas {disponivel} unidade(s) de {Rotulo(variacao)}.");

        // Rastreado, e por (carrinho, variacao): garante de quebra que a linha pertence a ESTE
        // carrinho, e nao a de outra pessoa que passou o id na URL.
        var item = await _carrinhos.ObterItemAsync(carrinho.Id, linha.IdVariacao, cancellationToken)
                   ?? throw new BusinessValidationException("Item nao encontrado neste carrinho.");

        item.Quantidade = dto.Quantidade;
        item.PrecoUnitarioSnapshotCentavos = variacao.PrecoEfetivoCentavos;

        await RenovarValidadeAsync(carrinho.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> RemoverItemAsync(
        IdentidadeCarrinho identidade,
        int idItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        var (idUsuario, carrinho) = await LocalizarOuFalharAsync(identidade, cancellationToken);

        var linha = LocalizarLinha(carrinho, idItem);

        var item = await _carrinhos.ObterItemAsync(carrinho.Id, linha.IdVariacao, cancellationToken);

        if (item is not null)
            _carrinhos.RemoverItem(item);

        await RenovarValidadeAsync(carrinho.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> EsvaziarAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        var (idUsuario, carrinho) = await LocalizarAsync(identidade, cancellationToken);

        if (carrinho is null)
            return Vazio();

        // Uma ida por linha porque o repositorio remove por entidade rastreada. O carrinho tem
        // poucas linhas por natureza; um DELETE em massa aqui economizaria pouco e abriria mao
        // do ChangeTracker que o resto do fluxo usa.
        foreach (var linha in carrinho.Itens.ToArray())
        {
            var item = await _carrinhos.ObterItemAsync(carrinho.Id, linha.IdVariacao, cancellationToken);

            if (item is not null)
                _carrinhos.RemoverItem(item);
        }

        var rastreado = await _carrinhos.ObterParaEdicaoAsync(carrinho.Id, cancellationToken);

        if (rastreado is not null)
        {
            // Esvaziar tambem solta o cupom: cupom com valor minimo preso a um carrinho vazio
            // reaparece aplicado quando o cliente volta a comprar e engana o total exibido.
            rastreado.IdCupom = null;
            rastreado.ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade);
            _carrinhos.Atualizar(rastreado);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Cupom
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> AplicarCupomAsync(
        IdentidadeCarrinho identidade,
        CupomAplicacaoDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);
        ArgumentNullException.ThrowIfNull(dto);
        BusinessValidationException.LancarSeVazio(dto.Codigo, "Informe o codigo do cupom.");

        var (idUsuario, carrinho) = await LocalizarOuFalharAsync(identidade, cancellationToken);

        if (carrinho.Itens.Count == 0)
            throw new BusinessValidationException("Adicione itens ao carrinho antes de aplicar um cupom.");

        var cupom = await _cupons.ObterPorCodigoAsync(dto.Codigo, cancellationToken)
                    ?? throw new BusinessValidationException("Cupom invalido ou expirado.");

        var subtotal = carrinho.Itens.Sum(i => PrecoAtual(i.Variacao) * i.Quantidade);

        var recusa = await ValidarCupomAsync(cupom, subtotal, idUsuario, carrinho.Itens, cancellationToken);

        if (recusa is not null)
            throw new BusinessValidationException(recusa);

        var rastreado = await _carrinhos.ObterParaEdicaoAsync(carrinho.Id, cancellationToken)
                        ?? throw new BusinessValidationException("Carrinho nao encontrado.");

        rastreado.IdCupom = cupom.Id;
        rastreado.ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade);
        _carrinhos.Atualizar(rastreado);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> RemoverCupomAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        var (idUsuario, carrinho) = await LocalizarAsync(identidade, cancellationToken);

        if (carrinho is null)
            return Vazio();

        var rastreado = await _carrinhos.ObterParaEdicaoAsync(carrinho.Id, cancellationToken);

        if (rastreado is not null)
        {
            rastreado.IdCupom = null;
            _carrinhos.Atualizar(rastreado);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await RecarregarAsync(carrinho.Uuid, idUsuario, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Merge no login
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<CarrinhoResponseDto> MesclarAsync(
        string uuidUsuario,
        string? chaveSessaoAnonima,
        CancellationToken cancellationToken = default)
    {
        BusinessValidationException.LancarSeVazio(uuidUsuario, "Sessao invalida para mesclar o carrinho.");

        var usuario = await _usuarios.ObterPorUuidAsync(uuidUsuario, cancellationToken)
                      ?? throw new UnauthorizedAccessException("Usuario do token nao existe mais.");

        var doUsuario = await _carrinhos.ObterAbertoDoUsuarioAsync(usuario.Id, cancellationToken);

        if (string.IsNullOrWhiteSpace(chaveSessaoAnonima))
        {
            return doUsuario is null
                ? Vazio()
                : await MontarAsync(doUsuario, usuario.Id, cancellationToken);
        }

        var anonimo = await _carrinhos.ObterAbertoPorSessaoAsync(chaveSessaoAnonima, cancellationToken);

        if (anonimo is null || anonimo.IdUsuario is not null)
        {
            return doUsuario is null
                ? Vazio()
                : await MontarAsync(doUsuario, usuario.Id, cancellationToken);
        }

        // Caminho barato: o usuario nao tinha carrinho aberto, entao o anonimo simplesmente
        // troca de dono. Zero copia de linha, zero risco de perder item por indisponibilidade.
        if (doUsuario is null)
        {
            var adotado = await _carrinhos.ObterParaEdicaoAsync(anonimo.Id, cancellationToken)
                          ?? throw new BusinessValidationException("Carrinho anonimo nao encontrado.");

            adotado.IdUsuario = usuario.Id;
            adotado.ChaveSessao = null;
            adotado.ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade);
            _carrinhos.Atualizar(adotado);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await RecarregarAsync(anonimo.Uuid, usuario.Id, cancellationToken);
        }

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        foreach (var linhaAnonima in anonimo.Itens.ToArray())
        {
            var disponivel = Disponivel(linhaAnonima.Variacao);
            var precoAtual = PrecoAtual(linhaAnonima.Variacao);

            var destino = await _carrinhos.ObterItemAsync(
                doUsuario.Id, linhaAnonima.IdVariacao, cancellationToken);

            if (destino is null)
            {
                // Item que so existia no anonimo. Entra limitado ao disponivel; sem saldo, nao
                // entra — copiar uma linha que ja nasce esgotada so gera frustracao no checkout.
                var quantidade = Math.Min(Math.Min(linhaAnonima.Quantidade, disponivel), MaximoPorLinha);

                if (quantidade > 0)
                {
                    await _carrinhos.AdicionarItemAsync(
                        new CarrinhoItemEntity
                        {
                            IdCarrinho = doUsuario.Id,
                            IdVariacao = linhaAnonima.IdVariacao,
                            Quantidade = quantidade,
                            PrecoUnitarioSnapshotCentavos = precoAtual,
                            DataAdicao = _relogio.UtcNow
                        },
                        cancellationToken);
                }
            }
            else
            {
                // Soma respeitando o disponivel. Quando nao ha saldo para a soma, a linha fica
                // com o que ja havia — nunca abaixo do que o cliente ja tinha escolhido.
                var somado = destino.Quantidade + linhaAnonima.Quantidade;
                var teto = Math.Min(MaximoPorLinha, disponivel > 0 ? disponivel : destino.Quantidade);

                destino.Quantidade = Math.Max(destino.Quantidade, Math.Min(somado, teto));
            }

            var itemAnonimo = await _carrinhos.ObterItemAsync(
                anonimo.Id, linhaAnonima.IdVariacao, cancellationToken);

            if (itemAnonimo is not null)
                _carrinhos.RemoverItem(itemAnonimo);
        }

        var alvo = await _carrinhos.ObterParaEdicaoAsync(doUsuario.Id, cancellationToken);

        if (alvo is not null)
        {
            // Cupom digitado antes do login nao se perde, mas nunca sobrepoe um ja aplicado.
            if (alvo.IdCupom is null && anonimo.IdCupom is not null)
                alvo.IdCupom = anonimo.IdCupom;

            alvo.ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade);
            _carrinhos.Atualizar(alvo);
        }

        var origem = await _carrinhos.ObterParaEdicaoAsync(anonimo.Id, cancellationToken);

        if (origem is not null)
        {
            // Sai de Aberto para liberar o slot do indice unico parcial por chave de sessao.
            // Abandonado e nao Convertido: nao virou pedido, foi absorvido.
            origem.Status = StatusCarrinho.Abandonado;
            origem.ChaveSessao = null;
            origem.IdCupom = null;
            _carrinhos.Atualizar(origem);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return await RecarregarAsync(doUsuario.Uuid, usuario.Id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Resolucao de carrinho
    // ------------------------------------------------------------------

    /// <summary>
    /// Acha o carrinho da identidade. Usuario autenticado tem precedencia sobre cookie: depois
    /// do login o cookie antigo pode continuar no navegador, e deixar ele ganhar faria o
    /// cliente logado ver o carrinho do visitante que ele era.
    /// </summary>
    private async Task<(int? IdUsuario, CarrinhoEntity? Carrinho)> LocalizarAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken)
    {
        if (identidade.Autenticado)
        {
            var usuario = await _usuarios.ObterPorUuidAsync(identidade.UuidUsuario!, cancellationToken)
                          ?? throw new UnauthorizedAccessException("Usuario do token nao existe mais.");

            return (usuario.Id, await _carrinhos.ObterAbertoDoUsuarioAsync(usuario.Id, cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(identidade.ChaveSessao))
            return (null, null);

        return (null, await _carrinhos.ObterAbertoPorSessaoAsync(identidade.ChaveSessao, cancellationToken));
    }

    private async Task<(int? IdUsuario, CarrinhoEntity Carrinho)> LocalizarOuFalharAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken)
    {
        var (idUsuario, carrinho) = await LocalizarAsync(identidade, cancellationToken);

        if (carrinho is null)
            throw new BusinessValidationException("Nao ha carrinho aberto para esta sessao.");

        return (idUsuario, carrinho);
    }

    private async Task<(int? IdUsuario, CarrinhoEntity Carrinho)> LocalizarOuCriarAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken)
    {
        var (idUsuario, carrinho) = await LocalizarAsync(identidade, cancellationToken);

        if (carrinho is not null)
            return (idUsuario, carrinho);

        if (idUsuario is null && string.IsNullOrWhiteSpace(identidade.ChaveSessao))
            throw new BusinessValidationException(
                "Nao foi possivel identificar a sessao do carrinho. Habilite os cookies do site.");

        var agora = _relogio.UtcNow;

        var novo = new CarrinhoEntity
        {
            // Uuid com hifen, formato UNICO no sistema. O repo de referencia gerava com e sem
            // hifen em caminhos diferentes e as duas formas nunca casavam.
            Uuid = Guid.NewGuid().ToString(),
            IdUsuario = idUsuario,
            ChaveSessao = idUsuario is null ? identidade.ChaveSessao : null,
            Status = StatusCarrinho.Aberto,
            ExpiraEm = agora.AddDays(DiasValidade)
        };

        await _carrinhos.AdicionarAsync(novo, cancellationToken);

        // Salva ja: as operacoes seguintes precisam do Id gerado para pendurar as linhas.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (idUsuario, novo);
    }

    private async Task RenovarValidadeAsync(int idCarrinho, CancellationToken cancellationToken)
    {
        var rastreado = await _carrinhos.ObterParaEdicaoAsync(idCarrinho, cancellationToken);

        if (rastreado is null)
            return;

        rastreado.ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade);
        _carrinhos.Atualizar(rastreado);
    }

    /// <summary>
    /// Le a linha pelo id vindo da rota a partir do carrinho JA resolvido pela identidade.
    /// Buscar o item direto por id deixaria alterar a linha do carrinho de outra pessoa.
    /// </summary>
    private static CarrinhoItemEntity LocalizarLinha(CarrinhoEntity carrinho, int idItem) =>
        carrinho.Itens.FirstOrDefault(i => i.Id == idItem)
        ?? throw new BusinessValidationException("Item nao encontrado neste carrinho.");

    /// <summary>
    /// Rele o carrinho DEPOIS do commit, pelo uuid.
    ///
    /// A releitura nao e desperdicio: as instancias que estavam em memoria vieram de consultas
    /// sem rastreamento e nao refletem o que acabou de ser salvo. Devolver o grafo que o banco
    /// realmente tem e o que evita a tela mostrar um total que o proximo GET desmente.
    /// </summary>
    private async Task<CarrinhoResponseDto> RecarregarAsync(
        string uuid,
        int? idUsuario,
        CancellationToken cancellationToken)
    {
        var completo = await _carrinhos.ObterPorUuidAsync(uuid, cancellationToken);

        return completo is null
            ? Vazio()
            : await MontarAsync(completo, idUsuario, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Montagem do DTO
    // ------------------------------------------------------------------

    private async Task<CarrinhoResponseDto> MontarAsync(
        CarrinhoEntity carrinho,
        int? idUsuario,
        CancellationToken cancellationToken)
    {
        var itens = carrinho.Itens
            .OrderBy(i => i.Id)
            .Select(MapearItem)
            .ToArray();

        var subtotal = itens.Sum(i => i.TotalLinhaCentavos);

        var cupom = carrinho.Cupom;
        var desconto = 0;
        var freteGratis = false;
        string? aviso = null;

        if (cupom is not null)
        {
            // O cupom pode ter vencido, esgotado ou deixado de atingir o minimo depois de
            // aplicado. Avisar e melhor que sumir com o desconto sem explicacao.
            aviso = await ValidarCupomAsync(cupom, subtotal, idUsuario, carrinho.Itens, cancellationToken);

            if (aviso is null)
            {
                freteGratis = cupom.Tipo == TipoCupom.FreteGratis;
                desconto = CalcularDesconto(cupom, carrinho.Itens);
            }
        }

        return new CarrinhoResponseDto
        {
            Uuid = carrinho.Uuid,
            Itens = itens,
            QuantidadeItens = itens.Sum(i => i.Quantidade),
            SubtotalCentavos = subtotal,
            DescontoCentavos = desconto,
            TotalCentavos = Math.Max(0, subtotal - desconto),
            CodigoCupom = cupom?.Codigo,
            FreteGratisPorCupom = freteGratis,
            AvisoCupom = aviso,
            PossuiItemIndisponivel = itens.Any(i => i.Indisponivel || i.QuantidadeAcimaDoDisponivel),
            PossuiPrecoAlterado = itens.Any(i => i.PrecoAlterado),
            PesoTotalGramas = itens.Sum(i => i.PesoGramas * i.Quantidade),
            ExpiraEm = carrinho.ExpiraEm
        };
    }

    private static CarrinhoItemResponseDto MapearItem(CarrinhoItemEntity item)
    {
        var variacao = item.Variacao;
        var precoAtual = PrecoAtual(variacao);
        var disponivel = Disponivel(variacao);

        // Produto ou variacao desativados chegam aqui porque a consulta usa IgnoreQueryFilters
        // de proposito: a linha some calada seria pior — o cliente veria o total mudar sozinho.
        var ativo = variacao is { Ativo: true, Produto.Ativo: true };

        return new CarrinhoItemResponseDto
        {
            Id = item.Id,
            IdVariacao = item.IdVariacao,
            IdProduto = variacao?.IdProduto ?? 0,
            Sku = variacao?.Sku ?? string.Empty,
            NomeProduto = variacao?.Produto?.Nome ?? string.Empty,
            SlugProduto = variacao?.Produto?.Slug,
            Tamanho = variacao?.Tamanho?.Codigo,
            Cor = variacao?.Cor?.Nome,
            CorHexRgb = variacao?.Cor?.HexRgb,
            Quantidade = item.Quantidade,
            PrecoUnitarioSnapshotCentavos = item.PrecoUnitarioSnapshotCentavos,
            PrecoUnitarioAtualCentavos = precoAtual,
            PrecoAlterado = item.PrecoUnitarioSnapshotCentavos != precoAtual,

            // Total pelo preco ATUAL: e o que sera cobrado. Somar pelo snapshot mostraria um
            // total que o checkout nao honra.
            TotalLinhaCentavos = precoAtual * item.Quantidade,
            DisponivelEmEstoque = disponivel,
            Indisponivel = !ativo || disponivel <= 0,
            QuantidadeAcimaDoDisponivel = ativo && disponivel > 0 && item.Quantidade > disponivel,
            PesoGramas = variacao?.PesoGramas ?? 0
        };
    }

    // ------------------------------------------------------------------
    // Cupom: validacao e calculo (PREVIA — a autoridade e o checkout)
    // ------------------------------------------------------------------

    /// <summary>
    /// Devolve a mensagem de recusa, ou null quando o cupom vale.
    ///
    /// Cupom inexistente e cupom inativo devolvem a MESMA mensagem generica de proposito:
    /// diferenciar transforma o endpoint num oraculo para descobrir codigos validos por
    /// tentativa e erro.
    ///
    /// NAO valida IdColecaoRestrita: exige a tabela de juncao produto/colecao e o valor real
    /// e recalculado no checkout, que e quem cobra. Validar pela metade aqui daria a impressao
    /// de garantia que esta previa nao tem.
    /// </summary>
    private async Task<string?> ValidarCupomAsync(
        Cupom cupom,
        int subtotalCentavos,
        int? idUsuario,
        IEnumerable<CarrinhoItemEntity> itens,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        if (!cupom.Ativo || cupom.VigenciaInicio > agora || (cupom.VigenciaFim is not null && cupom.VigenciaFim < agora))
            return "Cupom invalido ou expirado.";

        if (cupom.UsoMaximoTotal is not null && cupom.UsosAtuais >= cupom.UsoMaximoTotal)
            return "Este cupom ja atingiu o limite de utilizacoes.";

        if (cupom.ValorMinimoPedidoCentavos is > 0 && subtotalCentavos < cupom.ValorMinimoPedidoCentavos)
            return $"Este cupom vale para pedidos a partir de {Reais(cupom.ValorMinimoPedidoCentavos.Value)}.";

        if (cupom.IdCategoriaRestrita is > 0
            && !itens.Any(i => i.Variacao?.Produto?.IdCategoria == cupom.IdCategoriaRestrita))
            return "Este cupom nao se aplica aos itens do seu carrinho.";

        if (idUsuario is null)
        {
            // Sem login nao da para contar uso por pessoa nem saber se e primeira compra. O
            // cupom continua exibido, e o checkout (que exige login) e quem barra de verdade.
            return cupom.PrimeiraCompraApenas
                ? "Entre na sua conta para usar este cupom."
                : null;
        }

        if (cupom.UsoMaximoPorUsuario > 0)
        {
            var usos = await _cupons.ContarUsosDoUsuarioAsync(cupom.Id, idUsuario.Value, cancellationToken);

            if (usos >= cupom.UsoMaximoPorUsuario)
                return "Voce ja utilizou este cupom.";
        }

        if (cupom.PrimeiraCompraApenas
            && await _usuarios.PossuiPedidoPagoAsync(idUsuario.Value, cancellationToken))
        {
            return "Este cupom e valido apenas na primeira compra.";
        }

        return null;
    }

    /// <summary>
    /// Desconto em centavos. FreteGratis devolve ZERO aqui de proposito: o desconto dele cai na
    /// linha de FRETE do pedido, e o custo real continua sendo debitado da carteira do Melhor
    /// Envio — tratar como percentual sobre itens inflaria o desconto e escondaria o custo.
    /// </summary>
    private static int CalcularDesconto(Cupom cupom, IEnumerable<CarrinhoItemEntity> itens)
    {
        // Base restrita: quando o cupom vale so para uma categoria, o percentual incide apenas
        // sobre os itens dessa categoria, nunca sobre o carrinho inteiro.
        var elegiveis = cupom.IdCategoriaRestrita is > 0
            ? itens.Where(i => i.Variacao?.Produto?.IdCategoria == cupom.IdCategoriaRestrita)
            : itens;

        var baseCalculo = elegiveis.Sum(i => PrecoAtual(i.Variacao) * i.Quantidade);

        if (baseCalculo <= 0)
            return 0;

        var desconto = cupom.Tipo switch
        {
            // Valor e o percentual multiplicado por 100 (1250 = 12,50 por cento). O divisor
            // 10000 = 100 (percentual) x 100 (a escala). Errar isso da desconto de 100x.
            TipoCupom.Percentual => (int)Math.Round(baseCalculo * cupom.Valor / 10000m, MidpointRounding.AwayFromZero),
            TipoCupom.ValorFixo => cupom.Valor,
            _ => 0
        };

        if (cupom.Tipo == TipoCupom.Percentual && cupom.DescontoMaximoCentavos is > 0)
            desconto = Math.Min(desconto, cupom.DescontoMaximoCentavos.Value);

        // Nunca maior que a base: desconto acima do valor dos itens viraria total negativo.
        return Math.Clamp(desconto, 0, baseCalculo);
    }

    // ------------------------------------------------------------------
    // Utilitarios
    // ------------------------------------------------------------------

    private static int PrecoAtual(ProdutoVariacao? variacao) => variacao?.PrecoEfetivoCentavos ?? 0;

    private static int Disponivel(ProdutoVariacao? variacao)
    {
        if (variacao is null || !variacao.Ativo)
            return 0;

        if (variacao.Produto is { Ativo: false })
            return 0;

        var disponivel = variacao.Estoque?.Disponivel ?? 0;
        return disponivel < 0 ? 0 : disponivel;
    }

    private static string Rotulo(ProdutoVariacao variacao)
    {
        var produto = variacao.Produto?.Nome ?? variacao.Sku;
        var tamanho = variacao.Tamanho?.Codigo;
        var cor = variacao.Cor?.Nome;

        if (!string.IsNullOrWhiteSpace(tamanho) && !string.IsNullOrWhiteSpace(cor))
            return $"{produto} tamanho {tamanho} em {cor}";

        return string.IsNullOrWhiteSpace(tamanho) ? produto : $"{produto} tamanho {tamanho}";
    }

    /// <summary>
    /// Formata centavos como "R$ 1.234,56" SEM depender de cultura instalada.
    ///
    /// CultureInfo("pt-BR") lanca quando a imagem sobe com InvariantGlobalization, que e o
    /// default de varias imagens do runtime .NET. Trocar os separadores a mao e feio, mas nao
    /// derruba a mensagem de erro de um cupom por causa de configuracao de container.
    /// </summary>
    private static string Reais(int centavos)
    {
        var negativo = centavos < 0;
        var absoluto = Math.Abs(centavos);

        // Parte inteira e centavos tratados separados: assim o unico formato usado e "#,##0"
        // sobre um int, cujo separador de milhar invariante e a virgula — trocada por ponto em
        // seguida. Sem decimal, sem cultura carregada, sem surpresa em container invariante.
        var inteiro = (absoluto / 100).ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.');
        var fracao = (absoluto % 100).ToString("00", CultureInfo.InvariantCulture);

        return $"{(negativo ? "-" : string.Empty)}R$ {inteiro},{fracao}";
    }

    private CarrinhoResponseDto Vazio() => new()
    {
        Uuid = string.Empty,
        Itens = [],
        ExpiraEm = _relogio.UtcNow.AddDays(DiasValidade)
    };
}
