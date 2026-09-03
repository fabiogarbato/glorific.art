namespace Glorific.Application.Models.MelhorEnvio;

/// <summary>
/// Entrada de POST {ME}/api/cart — passo 1 do EnvioProcessor (Pendente -> NoCarrinho).
/// A resposta traz o uuid da etiqueta, que e a chave de tudo daqui pra frente.
/// </summary>
public sealed record CarrinhoEnvioRequisicao
{
    /// <summary>service — id do servico vindo da cotacao. Obrigatorio, maior que zero.</summary>
    public required int IdServico { get; init; }

    /// <summary>agency — so exigido por Latam, Azul e Buslog. Null nos Correios/Jadlog.</summary>
    public int? IdAgencia { get; init; }

    /// <summary>from — a loja.</summary>
    public required ParteEnvioInfo Remetente { get; init; }

    /// <summary>to — o cliente, a partir do snapshot congelado no pedido.</summary>
    public required ParteEnvioInfo Destinatario { get; init; }

    /// <summary>Declaracao de conteudo. Uma linha por item do pedido.</summary>
    public IReadOnlyList<ProdutoDeclaradoInfo> Produtos { get; init; } = [];

    /// <summary>Um volume por CAIXA FISICA. Volume nao tem quantidade — repetir a caixa e o certo.</summary>
    public IReadOnlyList<VolumeEnvioInfo> Volumes { get; init; } = [];

    public required OpcoesEnvioInfo Opcoes { get; init; }
}

/// <summary>
/// Remetente ou destinatario da etiqueta (o mesmo objeto "from"/"to" do ME).
///
/// Bairro e obrigatorio de verdade: o ME rejeita district vazio, e essa e a razao de
/// PedidoEnderecoSnapshot.Bairro ser required no dominio.
/// </summary>
public sealed record ParteEnvioInfo
{
    public required string Nome { get; init; }

    public string? Email { get; init; }

    public string? Telefone { get; init; }

    /// <summary>document — CPF do destinatario, so digitos.</summary>
    public string? Documento { get; init; }

    /// <summary>company_document — CNPJ do remetente PJ, so digitos.</summary>
    public string? DocumentoEmpresa { get; init; }

    /// <summary>state_register — inscricao estadual. "ISENTO" quando nao ha.</summary>
    public string? InscricaoEstadual { get; init; }

    /// <summary>economic_activity_code — CNAE. Exigido pela LATAM Cargo.</summary>
    public string? CodigoAtividadeEconomica { get; init; }

    /// <summary>address.</summary>
    public required string Logradouro { get; init; }

    public required string Numero { get; init; }

    public string? Complemento { get; init; }

    /// <summary>district — nunca vazio.</summary>
    public required string Bairro { get; init; }

    public required string Cidade { get; init; }

    /// <summary>postal_code, so digitos.</summary>
    public required string Cep { get; init; }

    /// <summary>state_abbr — UF com duas letras.</summary>
    public required string Uf { get; init; }

    /// <summary>country_id.</summary>
    public string PaisId { get; init; } = "BR";

    /// <summary>note — observacao impressa na etiqueta.</summary>
    public string? Observacao { get; init; }
}

/// <summary>
/// Produto da declaracao de conteudo.
/// No fio, quantity e unitary_value do ME sao STRING; a formatacao fica na Infrastructure para
/// que dinheiro continue sendo centavos inteiro dentro do sistema.
/// </summary>
public sealed record ProdutoDeclaradoInfo
{
    /// <summary>Ex.: "Vestido Midi Linho - M / Terracota".</summary>
    public required string Nome { get; init; }

    public required int Quantidade { get; init; }

    public required int ValorUnitarioCentavos { get; init; }

    public decimal? PesoKg { get; init; }
}

/// <summary>Uma caixa fisica. Sem quantidade de proposito — ver <see cref="CarrinhoEnvioRequisicao.Volumes"/>.</summary>
public sealed record VolumeEnvioInfo
{
    public decimal AlturaCm { get; init; }

    public decimal LarguraCm { get; init; }

    public decimal ComprimentoCm { get; init; }

    public decimal PesoKg { get; init; }
}

/// <summary>options do POST /api/cart.</summary>
public sealed record OpcoesEnvioInfo
{
    /// <summary>platform — aparece no painel do ME. Usar "glorific.art".</summary>
    public string? Plataforma { get; init; }

    /// <summary>insurance_value — total declarado do pedido em centavos.</summary>
    public int ValorSeguradoCentavos { get; init; }

    /// <summary>receipt. Sempre enviado (bool nao-nulavel no microservico).</summary>
    public bool AvisoRecebimento { get; init; }

    /// <summary>own_hand. Sempre enviado.</summary>
    public bool MaoPropria { get; init; }

    /// <summary>reverse. Sempre enviado.</summary>
    public bool Reversa { get; init; }

    /// <summary>
    /// non_commercial. bool? de proposito: quando null o campo e OMITIDO do payload (comportamento
    /// testado no microservico). true = sem nota; false + <see cref="ChaveNfe"/> = envio comercial.
    /// </summary>
    public bool? NaoComercial { get; init; }

    /// <summary>
    /// tags[].tag = numero do pedido (GA-2026-000137). E o que casa a etiqueta com o pedido nas
    /// telas do Melhor Envio quando alguem precisa investigar manualmente.
    /// </summary>
    public IReadOnlyList<EtiquetaTagInfo> Tags { get; init; } = [];

    /// <summary>invoice.key — chave da NF-e, 44 digitos.</summary>
    public string? ChaveNfe { get; init; }

    /// <summary>invoice.xml_content.</summary>
    public string? XmlNfe { get; init; }

    /// <summary>dce.key — declaracao de conteudo eletronica.</summary>
    public string? ChaveDce { get; init; }
}

public sealed record EtiquetaTagInfo
{
    public required string Tag { get; init; }

    public string? Url { get; init; }
}

/// <summary>
/// Resposta 201 de POST {ME}/api/cart. MeOrderId e o uuid da etiqueta — persistir ANTES de
/// qualquer outra chamada, senao uma queda aqui deixa uma etiqueta orfa paga no ME.
/// </summary>
public sealed record CarrinhoEnvioResultado
{
    public required string MeOrderId { get; init; }

    public string? Protocolo { get; init; }

    /// <summary>Status cru do ME ("pending", "released", "posted"...).</summary>
    public string? Status { get; init; }

    public int? PrecoCentavos { get; init; }

    public int? IdServico { get; init; }

    /// <summary>Payload cru para gravar em envios.raw_ultima_resposta (jsonb).</summary>
    public string? RawJson { get; init; }
}
