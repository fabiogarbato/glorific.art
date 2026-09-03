using Glorific.Application.Common;
using Glorific.Application.DTO.Promocoes;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Enums;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Regras de cupom.
///
/// Tres decisoes estruturam este arquivo:
///
/// 1. FreteGratis nao e "cem por cento de desconto". E tipo proprio porque o custo real do frete
///    continua sendo pago ao Melhor Envio: o desconto zera a linha COBRADA do cliente
///    (Pedido.FreteCentavos) enquanto Envio.ValorCompradoCentavos segue registrando o que saiu da
///    carteira. Colapsar os dois num percentual apagaria a margem de frete do relatorio.
///
/// 2. Cupom restrito a categoria ou colecao desconta sobre a BASE ELEGIVEL, nao sobre o carrinho
///    inteiro. "20 por cento em vestidos" aplicado ao total do carrinho e o erro classico que
///    entrega desconto em cima da bolsa e do cinto que o cliente colocou junto.
///
/// 3. Validar e consumir sao caminhos separados. A leitura pode rodar a cada tecla; a escrita e um
///    UPDATE condicional que so acontece uma vez, no checkout.
/// </summary>
public class CupomService
    : GenericService<Cupom, CupomCreateDto, CupomUpdateDto, CupomResponseDto>, ICupomService
{
    /// <summary>Percentual e guardado multiplicado por 100 (1250 = 12,50 por cento).</summary>
    private const int FatorPercentual = 10_000;

    private readonly ICupomRepository _cupons;
    private readonly IUsuarioRepository _usuarios;
    private readonly IProdutoRepository _produtos;
    private readonly IClock _relogio;

    public CupomService(
        ICupomRepository cupons,
        IUsuarioRepository usuarios,
        IProdutoRepository produtos,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
        : base(cupons, unitOfWork, mapper, consulta)
    {
        _cupons = cupons ?? throw new ArgumentNullException(nameof(cupons));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _produtos = produtos ?? throw new ArgumentNullException(nameof(produtos));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    protected override string NomeEntidade => "Cupom";

    /// <summary>Cupom novo primeiro: o painel abre na promocao que acabou de ser criada.</summary>
    protected override IQueryable<Cupom> AplicarOrdenacao(IQueryable<Cupom> consulta) =>
        consulta.OrderByDescending(cupom => cupom.DataCriacao).ThenByDescending(cupom => cupom.Id);

    // ------------------------------------------------------------------
    // CRUD administrativo
    // ------------------------------------------------------------------

    protected override async Task AntesDeCriarAsync(
        Cupom entidade,
        CupomCreateDto dto,
        CancellationToken cancellationToken)
    {
        var codigo = Normalizar(dto.Codigo);

        ValidarCoerencia(
            dto.Tipo,
            dto.Valor,
            dto.DescontoMaximoCentavos,
            dto.VigenciaInicio,
            dto.VigenciaFim,
            dto.IdCategoriaRestrita,
            dto.IdColecaoRestrita);

        if (await _cupons.CodigoEmUsoAsync(codigo, null, cancellationToken))
            throw new BusinessValidationException($"Ja existe um cupom com o codigo {codigo}.");

        // O mapeamento ja normalizou; a atribuicao aqui e a garantia de que um mapeamento
        // reescrito no futuro nao volte a gravar codigo em caixa mista contra o indice unico.
        entidade.Codigo = codigo;
    }

    protected override async Task AntesDeAtualizarAsync(
        Cupom entidade,
        CupomUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var codigo = Normalizar(dto.Codigo);

        ValidarCoerencia(
            dto.Tipo,
            dto.Valor,
            dto.DescontoMaximoCentavos,
            dto.VigenciaInicio,
            dto.VigenciaFim,
            dto.IdCategoriaRestrita,
            dto.IdColecaoRestrita);

        if (await _cupons.CodigoEmUsoAsync(codigo, entidade.Id, cancellationToken))
            throw new BusinessValidationException($"Ja existe outro cupom com o codigo {codigo}.");
    }

    /// <summary>
    /// Cupom que ja foi usado nao e apagado: cupons_usos e Pedido.IdCupom apontam para ele, e o
    /// relatorio de investimento em promocao depende dessa linha existir. Desative em vez de excluir.
    /// </summary>
    protected override async Task AntesDeRemoverAsync(Cupom entidade, CancellationToken cancellationToken)
    {
        var possuiUso = await Consulta.AlgumAsync(
            _cupons.Query().Where(cupom => cupom.Id == entidade.Id).SelectMany(cupom => cupom.Usos),
            cancellationToken);

        if (possuiUso || entidade.UsosAtuais > 0)
            throw new BusinessValidationException(
                "Este cupom ja foi utilizado e nao pode ser excluido. Desative-o para tirar de circulacao.");
    }

    /// <inheritdoc />
    public async Task<CupomResponseDto> ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        var normalizado = Normalizar(codigo);

        var cupom = await _cupons.ObterPorCodigoAsync(normalizado, cancellationToken)
            ?? throw new EntityNotFoundException(NomeEntidade, normalizado);

        return Mapear(cupom);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CupomResponseDto>> ListarAdminAsync(
        string? busca,
        bool? ativo,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = _cupons.Query();

        if (ativo is not null)
            consulta = consulta.Where(cupom => cupom.Ativo == ativo.Value);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // O codigo esta sempre em maiusculas no banco; comparar cru evita LOWER() na coluna
            // indexada e o consequente seq scan da tabela de cupons.
            var termo = busca.Trim();
            var termoCodigo = termo.ToUpperInvariant();

            consulta = consulta.Where(cupom =>
                cupom.Codigo.Contains(termoCodigo)
                || (cupom.Descricao != null && cupom.Descricao.Contains(termo)));
        }

        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<CupomResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = AplicarOrdenacao(consulta).Skip(requisicao.Skip).Take(requisicao.Take);

        var cupons = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<CupomResponseDto>.Criar([.. cupons.Select(Mapear)], requisicao, total);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CupomUsoResponseDto>> ListarUsosAsync(
        int idCupom,
        PageRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        if (!await _cupons.ExisteAsync(idCupom, cancellationToken))
            throw new EntityNotFoundException(NomeEntidade, idCupom);

        // SelectMany sobre a navegacao vira JOIN no banco. O ledger nunca e materializado inteiro:
        // um cupom de campanha grande tem dezenas de milhares de linhas.
        var consulta = _cupons.Query()
            .Where(cupom => cupom.Id == idCupom)
            .SelectMany(cupom => cupom.Usos);

        var total = await Consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<CupomUsoResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        var pagina = consulta
            .OrderByDescending(uso => uso.DataUso)
            .ThenByDescending(uso => uso.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(uso => new CupomUsoResponseDto
            {
                Id = uso.Id,
                IdCupom = uso.IdCupom,
                IdUsuario = uso.IdUsuario,
                EmailUsuario = uso.Usuario.Email,
                NomeUsuario = uso.Usuario.NomeCompleto,
                IdPedido = uso.IdPedido,
                NumeroPedido = uso.Pedido.Numero,
                ValorDescontadoCentavos = uso.ValorDescontadoCentavos,
                DataUso = uso.DataUso
            });

        var itens = await Consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<CupomUsoResponseDto>.Criar(itens, requisicao, total);
    }

    // ------------------------------------------------------------------
    // Validacao e consumo
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<Resultado<CupomAplicadoDto>> ValidarAsync(
        CupomValidacaoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        if (string.IsNullOrWhiteSpace(requisicao.Codigo))
            return Resultado<CupomAplicadoDto>.Falha("Informe o codigo do cupom.", "cupom_vazio");

        var codigo = Normalizar(requisicao.Codigo);

        var cupom = await _cupons.ObterPorCodigoAsync(codigo, cancellationToken);

        // Mensagem igual para inexistente e inativo de proposito: diferenciar as duas transforma o
        // endpoint num oraculo para descobrir quais cupons existem.
        if (cupom is null || !cupom.Ativo)
            return Resultado<CupomAplicadoDto>.Falha("Cupom invalido ou indisponivel.", "cupom_invalido");

        var agora = _relogio.UtcNow;

        if (cupom.VigenciaInicio > agora)
            return Resultado<CupomAplicadoDto>.Falha("Este cupom ainda nao esta valido.", "cupom_fora_de_vigencia");

        if (cupom.VigenciaFim is { } fim && fim < agora)
            return Resultado<CupomAplicadoDto>.Falha("Este cupom expirou.", "cupom_expirado");

        if (cupom.UsoMaximoTotal is { } tetoTotal && cupom.UsosAtuais >= tetoTotal)
            return Resultado<CupomAplicadoDto>.Falha("Este cupom se esgotou.", "cupom_esgotado");

        if (requisicao.IdUsuario <= 0)
            return Resultado<CupomAplicadoDto>.Falha("Entre na sua conta para usar um cupom.", "cupom_exige_login");

        var usosDoUsuario = await _cupons.ContarUsosDoUsuarioAsync(
            cupom.Id, requisicao.IdUsuario, cancellationToken);

        if (usosDoUsuario >= cupom.UsoMaximoPorUsuario)
            return Resultado<CupomAplicadoDto>.Falha(
                "Voce ja utilizou este cupom o numero maximo de vezes.", "cupom_limite_do_usuario");

        if (cupom.PrimeiraCompraApenas
            && await _usuarios.PossuiPedidoPagoAsync(requisicao.IdUsuario, cancellationToken))
        {
            return Resultado<CupomAplicadoDto>.Falha(
                "Este cupom vale apenas na primeira compra.", "cupom_primeira_compra");
        }

        if (cupom.ValorMinimoPedidoCentavos is { } minimo && requisicao.SubtotalCentavos < minimo)
            return Resultado<CupomAplicadoDto>.Falha(
                $"Este cupom exige pedido de no minimo {Reais(minimo)}.", "cupom_valor_minimo");

        var baseElegivel = await CalcularBaseElegivelAsync(cupom, requisicao, cancellationToken);

        if (baseElegivel <= 0)
            return Resultado<CupomAplicadoDto>.Falha(
                "Este cupom nao se aplica aos itens do seu carrinho.", "cupom_sem_item_elegivel");

        return MontarAplicado(cupom, requisicao, baseElegivel);
    }

    /// <inheritdoc />
    public async Task<Resultado<CupomAplicadoDto>> ConsumirAsync(
        CupomValidacaoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        var validacao = await ValidarAsync(requisicao, cancellationToken);

        if (validacao.Falhou)
            return validacao;

        var aplicado = validacao.Valor!;

        // UPDATE ... WHERE usos_atuais < uso_maximo_total. Ler e depois incrementar deixaria dois
        // checkouts simultaneos consumirem o mesmo ultimo uso do cupom dos primeiros cem.
        var consumiu = await _cupons.TentarConsumirUsoAsync(aplicado.IdCupom, cancellationToken);

        if (!consumiu)
            return Resultado<CupomAplicadoDto>.Falha("Este cupom se esgotou.", "cupom_esgotado");

        return Resultado<CupomAplicadoDto>.Ok(aplicado);
    }

    /// <inheritdoc />
    public async Task RegistrarUsoAsync(
        int idCupom,
        int idUsuario,
        int idPedido,
        int valorDescontadoCentavos,
        CancellationToken cancellationToken = default)
    {
        var uso = new CupomUso
        {
            IdCupom = idCupom,
            IdUsuario = idUsuario,
            IdPedido = idPedido,
            ValorDescontadoCentavos = valorDescontadoCentavos < 0 ? 0 : valorDescontadoCentavos,
            DataUso = _relogio.UtcNow
        };

        await _cupons.RegistrarUsoAsync(uso, cancellationToken);
    }

    /// <inheritdoc />
    public Task DevolverUsoAsync(int idCupom, CancellationToken cancellationToken = default) =>
        _cupons.DevolverUsoAsync(idCupom, cancellationToken);

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Base sobre a qual o desconto incide.
    ///
    /// Sem restricao, e o subtotal. Com restricao de categoria ou colecao, e a soma apenas das
    /// linhas elegiveis — e por isso que os itens precisam vir na requisicao. A categoria casa
    /// tambem pelas filhas: restringir a "Vestidos" tem de valer para "Vestidos > Midi", senao a
    /// campanha morre na subcategoria sem ninguem perceber.
    /// </summary>
    private async Task<int> CalcularBaseElegivelAsync(
        Cupom cupom,
        CupomValidacaoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var temRestricao = cupom.IdCategoriaRestrita is not null || cupom.IdColecaoRestrita is not null;

        if (!temRestricao)
            return requisicao.SubtotalCentavos;

        if (requisicao.Itens.Count == 0)
            return 0;

        var idsProduto = requisicao.Itens.Select(item => item.IdProduto).Distinct().ToArray();

        IQueryable<Produto> consulta = _produtos.Query().Where(produto => idsProduto.Contains(produto.Id));

        if (cupom.IdCategoriaRestrita is { } idCategoria)
        {
            consulta = consulta.Where(produto =>
                produto.IdCategoria == idCategoria
                || produto.Categoria.IdCategoriaPai == idCategoria);
        }

        if (cupom.IdColecaoRestrita is { } idColecao)
        {
            consulta = consulta.Where(produto =>
                produto.Colecoes.Any(vinculo => vinculo.IdColecao == idColecao));
        }

        var elegiveis = await Consulta.ListarAsync(consulta.Select(produto => produto.Id), cancellationToken);

        if (elegiveis.Count == 0)
            return 0;

        var conjunto = elegiveis.ToHashSet();

        var soma = requisicao.Itens
            .Where(item => conjunto.Contains(item.IdProduto))
            .Sum(item => (long)item.TotalLinhaCentavos);

        return soma <= 0 ? 0 : (int)Math.Min(soma, requisicao.SubtotalCentavos);
    }

    /// <summary>Calculo do desconto por tipo, ja com teto e sem nunca ultrapassar a base.</summary>
    private static Resultado<CupomAplicadoDto> MontarAplicado(
        Cupom cupom,
        CupomValidacaoRequest requisicao,
        int baseElegivel)
    {
        var descontoProdutos = 0;
        var descontoFrete = 0;
        var freteGratis = false;

        switch (cupom.Tipo)
        {
            case TipoCupom.FreteGratis:
                // Zera a linha COBRADA do cliente. O custo real continua sendo pago ao Melhor
                // Envio e registrado em Envio.ValorCompradoCentavos.
                freteGratis = true;
                descontoFrete = requisicao.FreteCentavos < 0 ? 0 : requisicao.FreteCentavos;
                break;

            case TipoCupom.ValorFixo:
                descontoProdutos = Math.Min(cupom.Valor, baseElegivel);
                break;

            case TipoCupom.Percentual:
                // long antes de dividir: 100_000_000 centavos x 10_000 estoura int no meio da conta.
                // Divisao inteira trunca para baixo de proposito — arredondar para cima daria ao
                // cliente um centavo a mais do que a promocao anunciada.
                var bruto = (int)((long)baseElegivel * cupom.Valor / FatorPercentual);

                descontoProdutos = cupom.DescontoMaximoCentavos is { } teto && bruto > teto
                    ? teto
                    : bruto;

                descontoProdutos = Math.Min(descontoProdutos, baseElegivel);
                break;

            default:
                return Resultado<CupomAplicadoDto>.Falha("Tipo de cupom nao suportado.", "cupom_tipo_invalido");
        }

        if (descontoProdutos <= 0 && !freteGratis)
            return Resultado<CupomAplicadoDto>.Falha(
                "Este cupom nao gera desconto para o seu carrinho.", "cupom_sem_efeito");

        return Resultado<CupomAplicadoDto>.Ok(new CupomAplicadoDto
        {
            IdCupom = cupom.Id,
            Codigo = cupom.Codigo,
            Descricao = cupom.Descricao,
            Tipo = cupom.Tipo,
            DescontoProdutosCentavos = descontoProdutos < 0 ? 0 : descontoProdutos,
            DescontoFreteCentavos = descontoFrete,
            FreteGratis = freteGratis,
            BaseElegivelCentavos = baseElegivel
        });
    }

    /// <summary>
    /// Coerencia que DataAnnotation nao alcanca porque depende da combinacao de dois campos.
    /// </summary>
    private static void ValidarCoerencia(
        TipoCupom tipo,
        int valor,
        int? descontoMaximoCentavos,
        DateTime vigenciaInicio,
        DateTime? vigenciaFim,
        int? idCategoriaRestrita,
        int? idColecaoRestrita)
    {
        switch (tipo)
        {
            case TipoCupom.Percentual when valor is <= 0 or > FatorPercentual:
                throw new BusinessValidationException(
                    "O percentual deve ficar entre 0,01 e 100 por cento (valor de 1 a 10000).");

            case TipoCupom.ValorFixo when valor <= 0:
                throw new BusinessValidationException("O desconto em reais deve ser maior que zero.");
        }

        if (tipo != TipoCupom.Percentual && descontoMaximoCentavos is not null)
            throw new BusinessValidationException(
                "Teto de desconto so faz sentido em cupom percentual.");

        if (vigenciaFim is { } fim && fim <= vigenciaInicio)
            throw new BusinessValidationException("O fim da vigencia deve ser posterior ao inicio.");

        // Restringir a categoria E a colecao ao mesmo tempo cria um cupom que quase nunca casa e
        // que ninguem consegue depurar pela tela. Uma restricao por vez, explicita.
        if (idCategoriaRestrita is not null && idColecaoRestrita is not null)
            throw new BusinessValidationException(
                "Escolha restringir o cupom por categoria OU por colecao, nao pelos dois.");
    }

    private static string Normalizar(string? codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Formata centavos como reais para a MENSAGEM de erro.
    ///
    /// Nao usa o formato "C" com pt-BR de proposito: a imagem de runtime pode subir sem ICU
    /// (DOTNET_SYSTEM_GLOBALIZATION_INVARIANT), e ai o GetCultureInfo lanca — uma mensagem de
    /// validacao de cupom nao pode ser a linha que derruba a requisicao.
    /// </summary>
    private static string Reais(int centavos) =>
        "R$ " + (centavos / 100m)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            .Replace('.', ',');
}
