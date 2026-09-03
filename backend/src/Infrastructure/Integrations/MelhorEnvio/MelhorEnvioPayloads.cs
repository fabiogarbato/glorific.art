namespace Glorific.Infrastructure.Integrations.MelhorEnvio;

/// <summary>
/// Corpos ENVIADOS ao microservico integracaoMelhorEnvio.
///
/// Sao internal e vivem so aqui: nenhum deles atravessa a porta IMelhorEnvioClient. Os nomes
/// sao os do CONTRATO DO PARCEIRO (camelCase, ingles), e nao os nossos, justamente para o
/// mapeamento de fronteira ser visivel num arquivo so — e o unico lugar do sistema onde
/// "postalCode" e "unitaryValue" podem aparecer.
///
/// O microservico converte tudo isto para snake_case antes de falar com o Melhor Envio.
/// </summary>
internal sealed record CepPayload
{
    public required string PostalCode { get; init; }
}

/// <summary>Item de POST /api/shipment/calculate. Peso em KG, medidas em CM, valor em reais.</summary>
internal sealed record ProdutoCotacaoPayload
{
    public string? Id { get; init; }
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public decimal Length { get; init; }
    public decimal Weight { get; init; }
    public decimal InsuranceValue { get; init; }
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// Volume de POST /api/shipment/calculate.
/// Atencao: aqui o campo de valor declarado chama "insurance", e NAO "insurance_value" como em
/// products. Trocar os dois faz o ME cotar com seguro zero e a diferenca so aparece no sinistro.
/// </summary>
internal sealed record VolumeCotacaoPayload
{
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public decimal Length { get; init; }
    public decimal Weight { get; init; }
    public decimal Insurance { get; init; }
}

/// <summary>options da cotacao. Os dois bool sao nao-nulaveis: o contrato sempre os envia.</summary>
internal sealed record OpcoesCotacaoPayload
{
    public bool Receipt { get; init; }
    public bool OwnHand { get; init; }
}

/// <summary>
/// Corpo de POST /api/shipment/calculate.
/// Products e Volumes sao nulaveis e mutuamente exclusivos: enviar os dois e 400 no
/// microservico ("Envie 'products' OU 'volumes', nao os dois").
/// </summary>
internal sealed record CotacaoPayload
{
    public required CepPayload From { get; init; }
    public required CepPayload To { get; init; }
    public IReadOnlyList<ProdutoCotacaoPayload>? Products { get; init; }
    public IReadOnlyList<VolumeCotacaoPayload>? Volumes { get; init; }
    public OpcoesCotacaoPayload Options { get; init; } = new();

    /// <summary>CSV de ids de servico ("1,2,18"). Null = todos os habilitados na conta.</summary>
    public string? Services { get; init; }
}

/// <summary>Remetente ou destinatario de POST /api/cart.</summary>
internal sealed record PartePayload
{
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>CPF do destinatario.</summary>
    public string? Document { get; init; }

    /// <summary>CNPJ do remetente PJ.</summary>
    public string? CompanyDocument { get; init; }

    public string? StateRegister { get; init; }

    /// <summary>CNAE. Exigido pela LATAM Cargo, ignorado por Correios e Jadlog.</summary>
    public string? EconomicActivityCode { get; init; }

    public required string Address { get; init; }
    public required string Number { get; init; }
    public string? Complement { get; init; }

    /// <summary>Bairro. O ME RECUSA o carrinho com district vazio.</summary>
    public required string District { get; init; }

    public required string City { get; init; }
    public required string PostalCode { get; init; }

    /// <summary>UF com duas letras.</summary>
    public required string StateAbbr { get; init; }

    public string CountryId { get; init; } = "BR";
    public string? Note { get; init; }
}

/// <summary>
/// Declaracao de conteudo de POST /api/cart.
/// Quantity e UnitaryValue sao STRING no contrato do parceiro — nao e engano de tipagem.
/// </summary>
internal sealed record ProdutoDeclaradoPayload
{
    public required string Name { get; init; }
    public required string Quantity { get; init; }
    public required string UnitaryValue { get; init; }
    public decimal? Weight { get; init; }
}

/// <summary>Uma caixa fisica. Sem quantidade: duas caixas sao duas entradas na lista.</summary>
internal sealed record VolumePayload
{
    public decimal Height { get; init; }
    public decimal Width { get; init; }
    public decimal Length { get; init; }
    public decimal Weight { get; init; }
}

internal sealed record TagPayload
{
    public required string Tag { get; init; }
    public string? Url { get; init; }
}

/// <summary>invoice do POST /api/cart. Omitido inteiro quando nao ha nota.</summary>
internal sealed record NotaFiscalPayload
{
    public required string Key { get; init; }
    public string? XmlContent { get; init; }
}

/// <summary>options de POST /api/cart.</summary>
internal sealed record OpcoesCarrinhoPayload
{
    public string? Platform { get; init; }
    public string? Reminder { get; init; }
    public decimal? InsuranceValue { get; init; }
    public bool Receipt { get; init; }
    public bool OwnHand { get; init; }
    public bool Reverse { get; init; }

    /// <summary>
    /// bool? de proposito. Quando null o campo e OMITIDO do payload (WhenWritingNull) —
    /// comportamento coberto por teste no microservico. Enviar false sem chave de NF-e faz o
    /// ME exigir nota e recusar a etiqueta.
    /// </summary>
    public bool? NonCommercial { get; init; }

    public IReadOnlyList<TagPayload>? Tags { get; init; }
    public NotaFiscalPayload? Invoice { get; init; }
    public ChaveDcePayload? Dce { get; init; }
}

internal sealed record ChaveDcePayload
{
    public required string Key { get; init; }
}

/// <summary>Corpo de POST /api/cart (201) — insere o frete no carrinho do Melhor Envio.</summary>
internal sealed record CarrinhoPayload
{
    public required int Service { get; init; }
    public int? Agency { get; init; }
    public required PartePayload From { get; init; }
    public required PartePayload To { get; init; }
    public IReadOnlyList<ProdutoDeclaradoPayload> Products { get; init; } = [];
    public IReadOnlyList<VolumePayload> Volumes { get; init; } = [];
    public OpcoesCarrinhoPayload Options { get; init; } = new();
}

/// <summary>Corpo de /api/cart/checkout, /api/labels/generate, /api/shipment/tracking.</summary>
internal sealed record EtiquetasPayload
{
    public IReadOnlyList<string> Orders { get; init; } = [];
}

/// <summary>Corpo de POST /api/labels/print. Mode null = link privado (e o campo some).</summary>
internal sealed record ImpressaoPayload
{
    public IReadOnlyList<string> Orders { get; init; } = [];
    public string? Mode { get; init; }
}

/// <summary>Corpo de POST /api/shipment/cancel.</summary>
internal sealed record CancelamentoPayload
{
    public required CancelamentoOrdemPayload Order { get; init; }
}

internal sealed record CancelamentoOrdemPayload
{
    public required string Id { get; init; }

    /// <summary>Lista fechada do ME. "2" e o generico de desistencia usado em integracao.</summary>
    public string ReasonId { get; init; } = "2";

    public string? Description { get; init; }
}
