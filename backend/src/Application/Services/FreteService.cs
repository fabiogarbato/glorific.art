using System.Globalization;
using Glorific.Application.Common;
using Glorific.Application.DTO.Frete;
using Glorific.Application.Exceptions;
using Glorific.Application.Models.MelhorEnvio;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Config;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Application.Services;

/// <summary>
/// Cotacao de frete contra o microservico do Melhor Envio.
///
/// QUATRO DECISOES QUE MERECEM LEITURA:
///
/// 1. CACHE DE 2 MINUTOS POR (ORIGEM, DESTINO, ASSINATURA DOS ITENS). A cotacao leva de 2 a 5 s
///    e o MESMO cliente a dispara na pagina de produto, no carrinho e no checkout. Dois minutos
///    e curto o bastante para nao servir tabela velha e longo o bastante para cobrir a jornada.
///    O que entra no cache e a resposta CRUA normalizada do parceiro; frete gratis e prazo de
///    manuseio sao aplicados DEPOIS da leitura, porque dependem de configuracao que o admin
///    pode mudar a qualquer momento e nao valeria a pena invalidar o cache por isso.
///
/// 2. NADA DE PESO OU PRECO VINDO DO CLIENTE. O corpo traz id de variacao e quantidade; peso,
///    dimensao e valor declarado saem de produto_variacoes. E a diferenca entre cotar e deixar
///    o cliente escolher quanto quer pagar de frete.
///
/// 3. FALLBACK DE VOLUME POR FAIXA DE ITENS. Quando alguma variacao esta sem peso ou dimensao,
///    a cotacao deixa de ir por products[] e vai por UM volume unico, dimensionado pela caixa
///    padrao com a altura escalada pela quantidade de pecas. Sem isso o parceiro devolve 422 na
///    cara do cliente por causa de um cadastro incompleto — mas a diferenca de valor sai do
///    bolso da loja, entao o log registra o SKU faltante para o admin corrigir.
///
/// 4. FRETE GRATIS NAO ZERA O CUSTO. O valor cobrado do cliente vai a zero, ValorCotado
///    continua visivel, e o custo real segue sendo debitado da carteira do Melhor Envio na
///    compra da etiqueta.
/// </summary>
public class FreteService : IFreteService
{
    /// <summary>Janela do cache de cotacao. Ver decisao 1 no cabecalho.</summary>
    private static readonly TimeSpan JanelaCache = TimeSpan.FromMinutes(2);

    /// <summary>Cache da configuracao da loja: lida em toda cotacao e muda raramente.</summary>
    private static readonly TimeSpan JanelaCacheConfiguracao = TimeSpan.FromMinutes(2);

    private const string PrefixoCache = "frete:cotacao:";
    private const string ChaveCacheConfiguracao = "frete:configuracao-loja";

    /// <summary>Teto da altura da caixa de fallback, em cm. Acima disso o parceiro recusa.</summary>
    private const decimal AlturaMaximaFallbackCm = 100m;

    private readonly IMelhorEnvioClient _melhorEnvio;
    private readonly IProdutoVariacaoRepository _variacoes;
    private readonly IConfiguracaoLojaRepository _configuracoes;
    private readonly IMemoryCache _cache;
    private readonly FreteOptions _opcoes;
    private readonly ILogger<FreteService> _logger;

    public FreteService(
        IMelhorEnvioClient melhorEnvio,
        IProdutoVariacaoRepository variacoes,
        IConfiguracaoLojaRepository configuracoes,
        IMemoryCache cache,
        IOptions<FreteOptions> opcoes,
        ILogger<FreteService> logger)
    {
        _melhorEnvio = melhorEnvio ?? throw new ArgumentNullException(nameof(melhorEnvio));
        _variacoes = variacoes ?? throw new ArgumentNullException(nameof(variacoes));
        _configuracoes = configuracoes ?? throw new ArgumentNullException(nameof(configuracoes));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _opcoes = opcoes?.Value ?? throw new ArgumentNullException(nameof(opcoes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OpcaoFreteResponseDto>> CotarAsync(
        CotacaoFreteRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return CotarItensAsync(dto.Cep, dto.Itens, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpcaoFreteResponseDto>> CotarItensAsync(
        string cep,
        IReadOnlyCollection<ItemCotacaoDto> itens,
        CancellationToken cancellationToken = default)
    {
        var contexto = await PrepararAsync(cep, itens, cancellationToken);

        var cotacoes = await ObterCotacoesAsync(contexto, _opcoes.ServicosCotacao, cancellationToken);

        return Apresentar(cotacoes, contexto);
    }

    /// <inheritdoc />
    public async Task<OpcaoFreteResponseDto> RecotarServicoAsync(
        string cep,
        IReadOnlyCollection<ItemCotacaoDto> itens,
        int idServico,
        CancellationToken cancellationToken = default)
    {
        if (idServico <= 0)
            throw new BusinessValidationException("Escolha uma opcao de frete antes de continuar.");

        var contexto = await PrepararAsync(cep, itens, cancellationToken);

        // Reaproveita a cotacao completa (mesma chave de cache da tela do carrinho) e escolhe o
        // servico. E server-side dos dois jeitos, e evita pagar mais 2 a 5 s do parceiro DENTRO
        // da transacao do checkout — segurar lock de banco durante I/O de rede e o que
        // transforma um pico de trafego em fila de conexoes esgotada.
        var cotacoes = await ObterCotacoesAsync(contexto, _opcoes.ServicosCotacao, cancellationToken);

        var escolhida = Apresentar(cotacoes, contexto).FirstOrDefault(o => o.IdServico == idServico);

        return escolhida
               ?? throw new BusinessValidationException(
                   "Este frete nao esta mais disponivel. Refaca a cotacao e escolha outra opcao.");
    }

    // ------------------------------------------------------------------
    // Preparacao
    // ------------------------------------------------------------------

    /// <summary>
    /// Valida a entrada, carrega as variacoes do banco e resolve a configuracao efetiva.
    /// Tudo o que a cotacao precisa fica num objeto so, porque as tres operacoes publicas
    /// dependem exatamente do mesmo preparo.
    /// </summary>
    private async Task<ContextoCotacao> PrepararAsync(
        string cep,
        IReadOnlyCollection<ItemCotacaoDto> itens,
        CancellationToken cancellationToken)
    {
        var cepDestino = CepHelper.SomenteDigitos(cep);

        if (!CepHelper.Valido(cepDestino))
            throw new BusinessValidationException("Informe um CEP valido, com 8 digitos.");

        if (itens is null || itens.Count == 0)
            throw new BusinessValidationException("Informe ao menos um item para calcular o frete.");

        // Agrupa antes de consultar: o mesmo SKU repetido no corpo viraria duas linhas na
        // cotacao e o parceiro cobraria embalagem duas vezes.
        var agrupados = itens
            .Where(i => i.IdVariacao > 0 && i.Quantidade > 0)
            .GroupBy(i => i.IdVariacao)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade));

        if (agrupados.Count == 0)
            throw new BusinessValidationException("Informe ao menos um item valido para calcular o frete.");

        var variacoes = await _variacoes.ObterParaCheckoutAsync(agrupados.Keys.ToArray(), cancellationToken);
        var porId = variacoes.ToDictionary(v => v.Id);

        var ausentes = agrupados.Keys.Where(id => !porId.ContainsKey(id)).ToArray();

        if (ausentes.Length > 0)
            throw new BusinessValidationException(
                "Um dos itens nao esta mais disponivel na loja. Atualize o carrinho e tente novamente.");

        var configuracao = await ObterConfiguracaoAsync(cancellationToken);

        // A configuracao do painel manda, MAS so quando e valida. Testar apenas "esta preenchida"
        // deixaria o placeholder do seed (00000000) encobrir um Frete:CepOrigem valido vindo do
        // ambiente, e a loja ficava sem cotar frete com a configuracao do deploy correta.
        var cepDaConfiguracao = CepHelper.SomenteDigitos(configuracao?.CepOrigem);
        var cepOrigem = CepHelper.Valido(cepDaConfiguracao)
            ? cepDaConfiguracao
            : CepHelper.SomenteDigitos(_opcoes.CepOrigem);

        if (!CepHelper.Valido(cepOrigem))
        {
            // Erro de CONFIGURACAO, nao do cliente. Vira 400 com mensagem honesta porque um 500
            // generico aqui esconderia do operador a unica coisa que ele precisa corrigir.
            _logger.LogError(
                "Cotacao de frete impossivel: CEP de origem ausente ou invalido (configuracoes_loja e Frete:CepOrigem).");

            throw new BusinessValidationException(
                "Calculo de frete indisponivel: a loja esta sem CEP de origem configurado.");
        }

        var linhas = agrupados
            .Select(par => new LinhaCotacao(porId[par.Key], par.Value))
            .OrderBy(l => l.Variacao.Id)
            .ToArray();

        return new ContextoCotacao
        {
            CepOrigem = cepOrigem,
            CepDestino = cepDestino,
            Linhas = linhas,
            SubtotalCentavos = linhas.Sum(l => l.Variacao.PrecoEfetivoCentavos * l.Quantidade),
            Configuracao = configuracao
        };
    }

    // ------------------------------------------------------------------
    // Cotacao com cache
    // ------------------------------------------------------------------

    private async Task<IReadOnlyList<CotacaoFreteResultado>> ObterCotacoesAsync(
        ContextoCotacao contexto,
        IList<int> servicos,
        CancellationToken cancellationToken)
    {
        var chave = MontarChaveCache(contexto, servicos);

        if (_cache.TryGetValue(chave, out IReadOnlyList<CotacaoFreteResultado>? emCache) && emCache is not null)
        {
            _logger.LogDebug("Cotacao de frete servida do cache. Chave={Chave}", chave);
            return emCache;
        }

        var requisicao = MontarRequisicao(contexto, servicos);

        IReadOnlyList<CotacaoFreteResultado> resposta;

        try
        {
            resposta = await _melhorEnvio.CotarFreteAsync(requisicao, cancellationToken);
        }
        catch (MelhorEnvioApiException excecao) when (excecao.EhContaNaoConectada)
        {
            // Nao e erro do cliente: a conta do Melhor Envio perdeu a autorizacao OAuth e a loja
            // PAROU de conseguir cotar e despachar. Alerta operacional, e a excecao sobe para o
            // middleware transformar em 502 — 400 aqui esconderia o incidente atras de uma
            // mensagem de "dados invalidos".
            _logger.LogError(
                excecao,
                "ALERTA OPERACIONAL: a conta do Melhor Envio nao esta conectada. Reautorize em /api/auth/authorize.");

            throw;
        }

        // Itens indisponiveis chegam COM "error" preenchido em vez de sumirem. Na vitrine eles
        // sao descartados: exibir "transportadora indisponivel" como opcao de frete so confunde.
        var normalizada = resposta
            .Where(o => o.Disponivel && o.PrecoCentavos > 0)
            .OrderBy(o => o.PrecoCentavos)
            .ThenBy(o => o.PrazoDias ?? int.MaxValue)
            .ToArray();

        if (normalizada.Length == 0)
        {
            var motivos = resposta
                .Where(o => !o.Disponivel)
                .Select(o => $"{o.NomeServico}: {o.Erro}")
                .Take(5);

            _logger.LogWarning(
                "Nenhuma opcao de frete disponivel para {CepDestino}. Motivos do parceiro: {Motivos}",
                contexto.CepDestino,
                string.Join(" | ", motivos));
        }

        // So o resultado NORMALIZADO entra no cache; frete gratis e manuseio sao aplicados na
        // leitura, porque dependem de configuracao que o admin muda sem passar por aqui.
        _cache.Set(chave, (IReadOnlyList<CotacaoFreteResultado>)normalizada, JanelaCache);

        return normalizada;
    }

    /// <summary>
    /// Chave estavel: origem, destino, servicos e a assinatura dos itens (id e quantidade).
    /// A assinatura entra ORDENADA por id, senao o mesmo carrinho em ordem diferente geraria
    /// duas entradas de cache para a mesma cotacao.
    /// </summary>
    private static string MontarChaveCache(ContextoCotacao contexto, IList<int> servicos)
    {
        var assinatura = string.Join(
            ',',
            contexto.Linhas.Select(l => $"{l.Variacao.Id}x{l.Quantidade}"));

        var listaServicos = string.Join('.', servicos ?? Array.Empty<int>());

        return $"{PrefixoCache}{contexto.CepOrigem}:{contexto.CepDestino}:{listaServicos}:{assinatura}";
    }

    // ------------------------------------------------------------------
    // Montagem da carga
    // ------------------------------------------------------------------

    private CotacaoFreteRequisicao MontarRequisicao(ContextoCotacao contexto, IList<int> servicos)
    {
        var incompletas = contexto.Linhas
            .Where(l => !Completa(l.Variacao))
            .Select(l => l.Variacao.Sku)
            .ToArray();

        if (incompletas.Length == 0)
        {
            // Caminho normal: uma linha por variacao, com peso e medida REAIS do SKU. O parceiro
            // resolve o empacotamento e devolve os pacotes que montou.
            return new CotacaoFreteRequisicao
            {
                CepOrigem = contexto.CepOrigem,
                CepDestino = contexto.CepDestino,
                Produtos = contexto.Linhas.Select(l => new CotacaoProdutoInfo
                {
                    Id = l.Variacao.Id.ToString(CultureInfo.InvariantCulture),
                    LarguraCm = l.Variacao.LarguraCm,
                    AlturaCm = l.Variacao.AlturaCm,
                    ComprimentoCm = l.Variacao.ComprimentoCm,
                    PesoKg = FreteConversoes.GramasParaKg(l.Variacao.PesoGramas),
                    ValorSeguradoCentavos = l.Variacao.PrecoEfetivoCentavos * l.Quantidade,
                    Quantidade = l.Quantidade
                }).ToArray(),
                Servicos = servicos?.ToArray() ?? Array.Empty<int>()
            };
        }

        _logger.LogWarning(
            "Cotacao caiu no volume de fallback: SKU sem peso ou dimensao cadastrada ({Skus}). " +
            "O valor cotado pode divergir do custo real e a diferenca sai da loja.",
            string.Join(", ", incompletas));

        return new CotacaoFreteRequisicao
        {
            CepOrigem = contexto.CepOrigem,
            CepDestino = contexto.CepDestino,
            Volumes = new[] { MontarVolumeFallback(contexto) },
            Servicos = servicos?.ToArray() ?? Array.Empty<int>()
        };
    }

    /// <summary>
    /// Caixa unica por PEDIDO, dimensionada pela faixa de quantidade de pecas.
    ///
    /// Um volume por item seria pior que o fallback: o parceiro cobraria embalagem N vezes e a
    /// cotacao sairia muito acima do custo. As faixas escalam so a ALTURA porque e assim que
    /// roupa dobrada empilha numa caixa — largura e comprimento sao os da caixa padrao.
    /// </summary>
    private CotacaoVolumeInfo MontarVolumeFallback(ContextoCotacao contexto)
    {
        var pecas = contexto.Linhas.Sum(l => l.Quantidade);

        var multiplicador = pecas switch
        {
            <= 2 => 1m,
            <= 5 => 2m,
            <= 10 => 3m,
            _ => 4m
        };

        var caixa = _opcoes.VolumeFallback;
        var altura = Math.Min(caixa.AlturaCm * multiplicador, AlturaMaximaFallbackCm);

        // Peso real quando existe; peso da caixa padrao apenas para o SKU que nao tem cadastro.
        var pesoGramas = contexto.Linhas.Sum(l =>
            (l.Variacao.PesoGramas > 0 ? l.Variacao.PesoGramas : caixa.PesoGramas) * l.Quantidade);

        return new CotacaoVolumeInfo
        {
            AlturaCm = altura,
            LarguraCm = caixa.LarguraCm,
            ComprimentoCm = caixa.ComprimentoCm,
            PesoKg = FreteConversoes.GramasParaKg(pesoGramas),
            ValorSeguradoCentavos = contexto.SubtotalCentavos
        };
    }

    private static bool Completa(ProdutoVariacao variacao) =>
        variacao.PesoGramas > 0
        && variacao.AlturaCm > 0
        && variacao.LarguraCm > 0
        && variacao.ComprimentoCm > 0;

    // ------------------------------------------------------------------
    // Apresentacao (frete gratis + prazo de manuseio)
    // ------------------------------------------------------------------

    private IReadOnlyList<OpcaoFreteResponseDto> Apresentar(
        IReadOnlyList<CotacaoFreteResultado> cotacoes,
        ContextoCotacao contexto)
    {
        var manuseio = contexto.Configuracao?.PrazoManuseioDias ?? _opcoes.PrazoManuseioDias;

        // A configuracao da loja tem precedencia sobre o appsettings: o limite de frete gratis e
        // decisao comercial que o admin muda na campanha, sem redeploy.
        var limiteGratis = contexto.Configuracao?.FreteGratisAcimaDeCentavos
                           ?? _opcoes.FreteGratisAcimaDeCentavos;

        var gratis = limiteGratis > 0 && contexto.SubtotalCentavos >= limiteGratis;

        return [.. cotacoes.Select(o => new OpcaoFreteResponseDto
        {
            IdServico = o.IdServico,
            Servico = o.NomeServico ?? $"Servico {o.IdServico}",
            Transportadora = o.NomeTransportadora,
            LogoTransportadora = o.LogoTransportadora,
            ValorCentavos = gratis ? 0 : o.PrecoCentavos,
            ValorCotadoCentavos = o.PrecoCentavos,
            Gratis = gratis,

            // Prazo exibido = transportadora + manuseio da loja. Mostrar so o da transportadora
            // e prometer uma data que a expedicao nao cumpre.
            PrazoDias = o.PrazoDias is null ? null : o.PrazoDias + manuseio,
            PrazoTransportadoraDias = o.PrazoDias
        })];
    }

    // ------------------------------------------------------------------
    // Configuracao da loja
    // ------------------------------------------------------------------

    /// <summary>
    /// Configuracao da loja com cache curto: e lida em TODA cotacao e muda raramente. Sem o
    /// cache, cada cotacao paga uma consulta a mais so para descobrir o CEP de origem.
    /// </summary>
    private async Task<ConfiguracaoLoja?> ObterConfiguracaoAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ChaveCacheConfiguracao, out ConfiguracaoLoja? emCache))
            return emCache;

        var configuracao = await _configuracoes.ObterAsync(cancellationToken);

        _cache.Set(ChaveCacheConfiguracao, configuracao, JanelaCacheConfiguracao);

        return configuracao;
    }

    // ------------------------------------------------------------------
    // Estruturas internas
    // ------------------------------------------------------------------

    private sealed record LinhaCotacao(ProdutoVariacao Variacao, int Quantidade);

    private sealed record ContextoCotacao
    {
        public required string CepOrigem { get; init; }

        public required string CepDestino { get; init; }

        public IReadOnlyList<LinhaCotacao> Linhas { get; init; } = [];

        /// <summary>Valor dos itens: e o seguro declarado e a base da regra de frete gratis.</summary>
        public int SubtotalCentavos { get; init; }

        public ConfiguracaoLoja? Configuracao { get; init; }
    }
}
