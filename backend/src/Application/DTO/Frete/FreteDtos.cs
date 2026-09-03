using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Frete;

/// <summary>
/// Item a cotar. Sempre por VARIACAO, nunca por produto: peso e dimensao vivem no SKU, e
/// "Vestido P" e "Vestido GG" tem peso 15 a 20 por cento diferente.
/// </summary>
public sealed record ItemCotacaoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe a variacao (tamanho e cor) do produto.")]
    public int IdVariacao { get; init; }

    [Range(1, 100, ErrorMessage = "A quantidade deve estar entre 1 e 100.")]
    public int Quantidade { get; init; } = 1;
}

/// <summary>
/// Cotacao publica: pagina de produto e simulador do carrinho. Nao exige login.
///
/// Os itens vem por id de variacao e a quantidade vem do cliente, mas PRECO, PESO e DIMENSAO
/// sao lidos do banco pelo servico — nunca do corpo. Aceitar peso do cliente e aceitar frete
/// forjado.
/// </summary>
public sealed record CotacaoFreteRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o CEP de destino.")]
    [StringLength(9, MinimumLength = 8, ErrorMessage = "O CEP deve ter 8 digitos.")]
    public string Cep { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe ao menos um item para cotar o frete.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um item para cotar o frete.")]
    [MaxLength(50, ErrorMessage = "Cotacao limitada a 50 itens distintos.")]
    public IReadOnlyList<ItemCotacaoDto> Itens { get; init; } = [];
}

/// <summary>Cotacao do carrinho atual: os itens saem do carrinho do servidor, so o CEP vem do cliente.</summary>
public sealed record CotacaoCarrinhoRequestDto : CreateDto
{
    [Required(ErrorMessage = "Informe o CEP de destino.")]
    [StringLength(9, MinimumLength = 8, ErrorMessage = "O CEP deve ter 8 digitos.")]
    public string Cep { get; init; } = string.Empty;
}

/// <summary>
/// Uma opcao de frete pronta para a tela.
///
/// PrazoDias ja inclui o manuseio da loja: exibir so o prazo da transportadora e prometer
/// entrega que a expedicao nao cumpre.
/// </summary>
public sealed record OpcaoFreteResponseDto : ResponseDto
{
    /// <summary>Id do SERVICO no Melhor Envio. E o que o checkout envia de volta.</summary>
    public int IdServico { get; init; }

    public string Servico { get; init; } = string.Empty;

    public string? Transportadora { get; init; }

    public string? LogoTransportadora { get; init; }

    /// <summary>Valor a cobrar do cliente. Zero quando a regra de frete gratis se aplica.</summary>
    public int ValorCentavos { get; init; }

    /// <summary>Valor cotado antes da regra de frete gratis. Serve para exibir "de R$ X".</summary>
    public int ValorCotadoCentavos { get; init; }

    public bool Gratis { get; init; }

    /// <summary>Prazo da transportadora MAIS Frete:PrazoManuseioDias.</summary>
    public int? PrazoDias { get; init; }

    /// <summary>Prazo cru da transportadora, sem o manuseio.</summary>
    public int? PrazoTransportadoraDias { get; init; }
}
