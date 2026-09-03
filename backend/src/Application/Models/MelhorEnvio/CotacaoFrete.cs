namespace Glorific.Application.Models.MelhorEnvio;

/// <summary>
/// Entrada de POST {ME}/api/shipment/calculate.
///
/// Fronteira: aqui tudo tem nome nosso e unidade explicita no nome do campo. A traducao para o
/// contrato do microservico (entrada camelCase -> saida snake_case para o Melhor Envio) e
/// responsabilidade EXCLUSIVA do adaptador na Infrastructure. Nenhum JsonNode, JsonElement ou
/// tipo de HttpClient atravessa esta porta.
/// </summary>
public sealed record CotacaoFreteRequisicao
{
    /// <summary>CEP da loja, so digitos. Vem de Frete:CepOrigem.</summary>
    public required string CepOrigem { get; init; }

    /// <summary>CEP do cliente, so digitos.</summary>
    public required string CepDestino { get; init; }

    /// <summary>
    /// Produtos a cotar. O microservico exige products OU volumes, nunca os dois — enviar os
    /// dois e 400. Peso e dimensao saem SEMPRE de produto_variacoes, nunca de produtos.
    /// </summary>
    public IReadOnlyList<CotacaoProdutoInfo> Produtos { get; init; } = [];

    /// <summary>Alternativa a <see cref="Produtos"/>: caixa fechada ja montada.</summary>
    public IReadOnlyList<CotacaoVolumeInfo>? Volumes { get; init; }

    /// <summary>options.receipt — aviso de recebimento.</summary>
    public bool AvisoRecebimento { get; init; }

    /// <summary>options.ownHand — mao propria.</summary>
    public bool MaoPropria { get; init; }

    /// <summary>
    /// Ids de servico a consultar (Frete:ServicosCotacao). Vazio = todos os habilitados.
    /// Na RECOTACAO do checkout vai apenas o id escolhido pelo cliente (anti-fraude, G.2 passo 5).
    /// </summary>
    public IReadOnlyList<int> Servicos { get; init; } = [];
}

/// <summary>Item da cotacao. Uma linha por variacao do carrinho.</summary>
public sealed record CotacaoProdutoInfo
{
    /// <summary>Identificador nosso repassado ao ME apenas para correlacao (id da variacao).</summary>
    public string? Id { get; init; }

    public decimal LarguraCm { get; init; }

    public decimal AlturaCm { get; init; }

    public decimal ComprimentoCm { get; init; }

    /// <summary>Peso em KG decimal. O banco guarda gramas; a conversao mora na Infrastructure.</summary>
    public decimal PesoKg { get; init; }

    /// <summary>
    /// Valor declarado em centavos = preco da variacao x quantidade. O ME recebe reais decimais;
    /// aqui e centavos porque dinheiro nao trafega em double dentro do sistema.
    /// </summary>
    public int ValorSeguradoCentavos { get; init; }

    public int Quantidade { get; init; } = 1;
}

/// <summary>Volume ja embalado. Note que aqui o campo do ME e "insurance", nao "insurance_value".</summary>
public sealed record CotacaoVolumeInfo
{
    public decimal LarguraCm { get; init; }

    public decimal AlturaCm { get; init; }

    public decimal ComprimentoCm { get; init; }

    public decimal PesoKg { get; init; }

    public int ValorSeguradoCentavos { get; init; }
}

/// <summary>
/// Uma opcao de frete devolvida por /api/shipment/calculate.
///
/// Armadilhas do contrato real ja absorvidas aqui:
/// - o ME devolve ARRAY, mas objeto unico quando services tem um id so;
/// - price vem string em /calculate e numero em /cart;
/// - itens indisponiveis vem com "error" preenchido em vez de sumirem da lista.
/// O adaptador normaliza os dois primeiros; o terceiro fica visivel em <see cref="Erro"/> para
/// que o servico decida entre descartar (vitrine) e explicar (checkout).
/// </summary>
public sealed record CotacaoFreteResultado
{
    /// <summary>id do SERVICO no ME (1 = PAC, 2 = SEDEX...). E o que vai em service no /api/cart.</summary>
    public required int IdServico { get; init; }

    /// <summary>Nome do servico ("PAC", "SEDEX", ".Package").</summary>
    public string? NomeServico { get; init; }

    /// <summary>company.name.</summary>
    public string? NomeTransportadora { get; init; }

    /// <summary>company.picture — logo exibida na lista de fretes.</summary>
    public string? LogoTransportadora { get; init; }

    /// <summary>custom_price convertido para centavos. E o valor que cobramos do cliente.</summary>
    public int PrecoCentavos { get; init; }

    /// <summary>price (tabela cheia) em centavos, quando informado.</summary>
    public int? PrecoTabelaCentavos { get; init; }

    public int? DescontoCentavos { get; init; }

    /// <summary>custom_delivery_time em dias uteis. Somar Frete:PrazoManuseioDias antes de exibir.</summary>
    public int? PrazoDias { get; init; }

    /// <summary>Preenchido quando a transportadora esta indisponivel para a rota/pacote.</summary>
    public string? Erro { get; init; }

    public bool Disponivel => string.IsNullOrWhiteSpace(Erro);
}
