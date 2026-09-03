using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "Frete". Tudo o que o negocio precisa decidir sobre envio e que NAO e segredo de
/// integracao (isso fica em <see cref="MelhorEnvioOptions"/>).
/// </summary>
public sealed class FreteOptions
{
    public const string SectionName = "Frete";

    /// <summary>CEP de origem de toda cotacao. So digitos, 8 posicoes.</summary>
    [Required(ErrorMessage = "Frete:CepOrigem e obrigatorio.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "Frete:CepOrigem deve ter 8 digitos, sem hifen.")]
    public string CepOrigem { get; set; } = string.Empty;

    /// <summary>Dados do remetente impressos na etiqueta (o "from" do POST /api/cart).</summary>
    [Required(ErrorMessage = "Frete:Remetente e obrigatorio.")]
    public RemetenteOptions Remetente { get; set; } = new();

    /// <summary>
    /// Ids de servico consultados na cotacao publica (1 PAC, 2 SEDEX, 3 Jadlog .Package...).
    /// Lista fechada de proposito: cotar todos os servicos habilitados deixa opcoes que a loja
    /// nao quer vender aparecendo na vitrine.
    /// </summary>
    public IList<int> ServicosCotacao { get; set; } = [1, 2, 3, 4, 17, 18];

    /// <summary>
    /// Servicos que dispensam nota fiscal. Envio cujo servico esta FORA desta lista nasce em
    /// AguardandoNota e o worker nao o pega ate o admin informar a chave.
    ///
    /// Atencao de go-live: loja PJ de moda emite NF-e em praticamente todo pedido, entao esta
    /// lista tende a ficar vazia e AguardandoNota passa a ser o fluxo padrao.
    /// </summary>
    public IList<int> ServicosSemNota { get; set; } = [];

    /// <summary>
    /// Caixa usada quando a variacao nao tem dimensao cadastrada. Existe para que um cadastro
    /// incompleto vire uma cotacao aproximada em vez de um 422 do Melhor Envio na cara do
    /// cliente — mas o valor errado sai do bolso da loja, entao o cadastro continua sendo o certo.
    /// </summary>
    [Required]
    public VolumeFallbackOptions VolumeFallback { get; set; } = new();

    /// <summary>Dias uteis de manuseio somados ao prazo da transportadora antes de exibir.</summary>
    [Range(0, 30, ErrorMessage = "Frete:PrazoManuseioDias deve estar entre 0 e 30.")]
    public int PrazoManuseioDias { get; set; } = 1;

    /// <summary>
    /// Valor de pedido a partir do qual o frete e gratis, em centavos. Zero desliga a regra.
    /// O custo real continua sendo debitado da carteira do ME — por isso envios guarda cotado e
    /// comprado separados.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FreteGratisAcimaDeCentavos { get; set; }

    /// <summary>Plataforma informada ao ME (options.platform), visivel no painel do lojista.</summary>
    [Required]
    public string Plataforma { get; set; } = "glorific.art";

    /// <summary>Servicos como CSV, no formato que o campo "services" da cotacao espera.</summary>
    public string ServicosCotacaoCsv => string.Join(',', ServicosCotacao);

    /// <summary>Este servico exige nota fiscal antes da postagem?</summary>
    public bool ExigeNota(int idServico) => !ServicosSemNota.Contains(idServico);
}

/// <summary>
/// Remetente da etiqueta. Endereco completo, e nao so nome e documento, porque o POST /api/cart
/// do Melhor Envio exige logradouro, numero, bairro, cidade, CEP e UF no "from" — bairro vazio
/// e recusa na hora.
/// </summary>
public sealed class RemetenteOptions
{
    [Required(ErrorMessage = "Frete:Remetente:Nome e obrigatorio.")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>CNPJ (ou CPF) do remetente, so digitos. Vai em company_document.</summary>
    [Required(ErrorMessage = "Frete:Remetente:Documento e obrigatorio.")]
    [RegularExpression(@"^(\d{11}|\d{14})$", ErrorMessage = "Frete:Remetente:Documento deve ter 11 (CPF) ou 14 (CNPJ) digitos.")]
    public string Documento { get; set; } = string.Empty;

    /// <summary>Inscricao estadual. "ISENTO" quando nao ha — o campo nao pode ir vazio.</summary>
    public string InscricaoEstadual { get; set; } = "ISENTO";

    /// <summary>CNAE. Exigido pela LATAM Cargo; irrelevante para Correios e Jadlog.</summary>
    public string? CodigoAtividadeEconomica { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>Telefone com DDD, so digitos.</summary>
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Frete:Remetente:Logradouro e obrigatorio.")]
    public string Logradouro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Frete:Remetente:Numero e obrigatorio.")]
    public string Numero { get; set; } = string.Empty;

    public string? Complemento { get; set; }

    [Required(ErrorMessage = "Frete:Remetente:Bairro e obrigatorio (district nao pode ir vazio ao ME).")]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Frete:Remetente:Cidade e obrigatoria.")]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Frete:Remetente:Uf e obrigatoria.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Frete:Remetente:Uf deve ter 2 letras.")]
    public string Uf { get; set; } = string.Empty;
}

/// <summary>Caixa padrao. Dimensoes em cm, peso em gramas (a conversao para kg e da Infrastructure).</summary>
public sealed class VolumeFallbackOptions
{
    [Range(1, 200, ErrorMessage = "Frete:VolumeFallback:AlturaCm deve estar entre 1 e 200.")]
    public decimal AlturaCm { get; set; } = 8;

    [Range(1, 200, ErrorMessage = "Frete:VolumeFallback:LarguraCm deve estar entre 1 e 200.")]
    public decimal LarguraCm { get; set; } = 30;

    [Range(1, 200, ErrorMessage = "Frete:VolumeFallback:ComprimentoCm deve estar entre 1 e 200.")]
    public decimal ComprimentoCm { get; set; } = 40;

    [Range(1, 30000, ErrorMessage = "Frete:VolumeFallback:PesoGramas deve estar entre 1 e 30000.")]
    public int PesoGramas { get; set; } = 400;
}
