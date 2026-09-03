using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Carrinho;

/// <summary>
/// Quem e o dono do carrinho na requisicao atual.
///
/// Os dois campos convivem porque o carrinho nasce anonimo (cookie gl_cart) e e adotado no
/// login. UuidUsuario vem do token e NUNCA do corpo — carrinho identificado por id enviado
/// pelo cliente e o caminho direto para ler o carrinho de outra pessoa.
/// </summary>
public sealed record IdentidadeCarrinho
{
    /// <summary>usuarios.Uuid extraido da claim sub. Null em visitante anonimo.</summary>
    public string? UuidUsuario { get; init; }

    /// <summary>Cookie de sessao do visitante. O controller gera quando nao existe.</summary>
    public string? ChaveSessao { get; init; }

    public bool Autenticado => !string.IsNullOrWhiteSpace(UuidUsuario);
}

/// <summary>Uma linha do carrinho, ja resolvida para exibicao.</summary>
public sealed record CarrinhoItemResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdVariacao { get; init; }

    public int IdProduto { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string NomeProduto { get; init; } = string.Empty;

    public string? SlugProduto { get; init; }

    public string? Tamanho { get; init; }

    public string? Cor { get; init; }

    public string? CorHexRgb { get; init; }

    public int Quantidade { get; init; }

    /// <summary>Preco no instante em que o item entrou no carrinho.</summary>
    public int PrecoUnitarioSnapshotCentavos { get; init; }

    /// <summary>Preco vigente agora. E o que sera cobrado se o checkout acontecer.</summary>
    public int PrecoUnitarioAtualCentavos { get; init; }

    /// <summary>
    /// Snapshot diferente do preco atual. O carrinho NAO corrige sozinho: avisar e melhor que
    /// cobrar surpresa, e melhor que apagar a linha sem explicacao.
    /// </summary>
    public bool PrecoAlterado { get; init; }

    public int TotalLinhaCentavos { get; init; }

    /// <summary>Saldo vendavel agora. Carrinho nao reserva estoque: isto e informativo.</summary>
    public int DisponivelEmEstoque { get; init; }

    /// <summary>Variacao desativada, produto desativado ou sem saldo. Bloqueia o checkout.</summary>
    public bool Indisponivel { get; init; }

    /// <summary>Ha saldo, mas menos do que a quantidade pedida.</summary>
    public bool QuantidadeAcimaDoDisponivel { get; init; }

    public int PesoGramas { get; init; }
}

/// <summary>
/// O carrinho inteiro como o front desenha a tela.
///
/// Frete NAO entra aqui: ele depende do CEP e sai por /carrinho/frete. Misturar os dois faria
/// toda leitura de carrinho pagar uma cotacao de 2 a 5 s no parceiro.
/// </summary>
public sealed record CarrinhoResponseDto : ResponseDto
{
    /// <summary>Identificador publico. O id inteiro nunca sai para o front.</summary>
    public string Uuid { get; init; } = string.Empty;

    public IReadOnlyList<CarrinhoItemResponseDto> Itens { get; init; } = [];

    public int QuantidadeItens { get; init; }

    /// <summary>Soma de quantidade x preco ATUAL. Nao usa o snapshot.</summary>
    public int SubtotalCentavos { get; init; }

    /// <summary>Previa do desconto do cupom. A autoridade e o checkout, que recalcula tudo.</summary>
    public int DescontoCentavos { get; init; }

    public int TotalCentavos { get; init; }

    public string? CodigoCupom { get; init; }

    /// <summary>Cupom do tipo FreteGratis: o desconto entra na linha de frete, nao nos itens.</summary>
    public bool FreteGratisPorCupom { get; init; }

    /// <summary>Aviso quando o cupom aplicado deixou de valer (venceu, esgotou, valor minimo).</summary>
    public string? AvisoCupom { get; init; }

    /// <summary>Algum item indisponivel. O front bloqueia o botao de fechar pedido.</summary>
    public bool PossuiItemIndisponivel { get; init; }

    /// <summary>Algum item com preco alterado desde que entrou.</summary>
    public bool PossuiPrecoAlterado { get; init; }

    public int PesoTotalGramas { get; init; }

    public DateTime ExpiraEm { get; init; }
}

/// <summary>Adicao de item. A quantidade e somada quando a variacao ja esta no carrinho.</summary>
public sealed record CarrinhoItemCreateDto : CreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe a variacao (tamanho e cor) do produto.")]
    public int IdVariacao { get; init; }

    /// <summary>
    /// Teto de 20 por linha. Nao e regra de negocio inventada: sem teto, um "quantidade:
    /// 999999" na requisicao vira uma cotacao de frete absurda e um total que estoura o int.
    /// </summary>
    [Range(1, 20, ErrorMessage = "A quantidade deve estar entre 1 e 20.")]
    public int Quantidade { get; init; } = 1;
}

/// <summary>Alteracao de quantidade. Zero remove a linha.</summary>
public sealed record CarrinhoItemUpdateDto : UpdateDto
{
    [Range(0, 20, ErrorMessage = "A quantidade deve estar entre 0 e 20.")]
    public int Quantidade { get; init; }
}

/// <summary>Aplicacao de cupom no carrinho. O codigo e normalizado em maiusculas pelo servico.</summary>
public sealed record CupomAplicacaoDto : CreateDto
{
    [Required(ErrorMessage = "Informe o codigo do cupom.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "O codigo deve ter entre 2 e 50 caracteres.")]
    public string Codigo { get; init; } = string.Empty;
}
