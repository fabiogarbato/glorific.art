using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Pedidos;

/// <summary>
/// Linha da listagem "meus pedidos" e da listagem do painel. Enxuta de proposito: a tela de
/// lista nao carrega itens, endereco nem historico.
/// </summary>
public sealed record PedidoResumoResponseDto
{
    public required string Uuid { get; init; }

    public required string Numero { get; init; }

    public required string Status { get; init; }

    public int TotalCentavos { get; init; }

    public int QuantidadeItens { get; init; }

    /// <summary>Miniatura do primeiro item, ja congelada no pedido (nunca lida do catalogo).</summary>
    public string? ImagemUrl { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? DataPagamento { get; init; }

    public string? CodigoRastreio { get; init; }
}

/// <summary>
/// Item do pedido. TODOS os campos vem de snapshot gravado no momento da compra: renomear o
/// produto ou trocar a foto no catalogo nao pode reescrever recibo antigo.
/// </summary>
public sealed record PedidoItemResponseDto
{
    // Sem "required" nestas quatro projecoes (item, endereco, historico e evento de rastreio):
    // elas sao materializadas pelo Mapster, que constroi o objeto por arvore de expressao. Membro
    // required nesse caminho depende de detalhe de geracao de codigo do mapeador, e o preco de
    // errar e o boot cair no Compile. O valor default vazio nunca chega ao cliente porque a
    // origem e sempre um snapshot obrigatorio no banco.
    public string Sku { get; init; } = string.Empty;

    public string NomeProduto { get; init; } = string.Empty;

    public string Tamanho { get; init; } = string.Empty;

    public string Cor { get; init; } = string.Empty;

    public string? ImagemUrl { get; init; }

    public int Quantidade { get; init; }

    public int PrecoUnitarioCentavos { get; init; }

    public int DescontoUnitarioCentavos { get; init; }

    public int TotalLinhaCentavos { get; init; }
}

/// <summary>Endereco congelado no pedido, nao o endereco atual do cadastro do cliente.</summary>
public sealed record PedidoEnderecoResponseDto
{
    public string Destinatario { get; init; } = string.Empty;

    public string TelefoneContato { get; init; } = string.Empty;

    public string Cep { get; init; } = string.Empty;

    public string Logradouro { get; init; } = string.Empty;

    public string Numero { get; init; } = string.Empty;

    public string? Complemento { get; init; }

    public string Bairro { get; init; } = string.Empty;

    public string Cidade { get; init; } = string.Empty;

    public string Uf { get; init; } = string.Empty;
}

public sealed record PedidoPagamentoResponseDto
{
    public required string Provedor { get; init; }

    public string? Metodo { get; init; }

    public required string Status { get; init; }

    public int ValorCentavos { get; init; }

    public int? Parcelas { get; init; }

    /// <summary>Link do checkout hospedado. So faz sentido enquanto o pagamento esta pendente.</summary>
    public string? PaymentUrl { get; init; }

    public string? QrCodePix { get; init; }

    public string? LinhaDigitavel { get; init; }

    public DateTime? ExpiraEm { get; init; }

    public DateTime? DataConfirmacao { get; init; }
}

public sealed record PedidoEnvioResponseDto
{
    public required string Status { get; init; }

    public string? Transportadora { get; init; }

    public string? Servico { get; init; }

    public string? CodigoRastreio { get; init; }

    /// <summary>Nunca exposto ao cliente final — so no painel. Ver PedidoService.</summary>
    public string? UrlEtiqueta { get; init; }

    public int? PrazoDias { get; init; }

    public DateTime? DataAlteracao { get; init; }
}

public sealed record PedidoHistoricoResponseDto
{
    public string? StatusAnterior { get; init; }

    public string StatusNovo { get; init; } = string.Empty;

    public string? Observacao { get; init; }

    public DateTime DataAlteracao { get; init; }
}

/// <summary>Detalhe do pedido: o recibo. Tudo que aparece aqui e snapshot ou estado proprio.</summary>
public sealed record PedidoResponseDto
{
    public required string Uuid { get; init; }

    public required string Numero { get; init; }

    public required string Status { get; init; }

    public int SubtotalCentavos { get; init; }

    public int DescontoCupomCentavos { get; init; }

    public int FreteCentavos { get; init; }

    public int TotalCentavos { get; init; }

    public string? CodigoCupom { get; init; }

    public string? TransportadoraFrete { get; init; }

    public string? ServicoFrete { get; init; }

    public int? PrazoFreteDias { get; init; }

    public string? ObservacaoCliente { get; init; }

    public string? MotivoCancelamento { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? DataPagamento { get; init; }

    public DateTime? DataEnvio { get; init; }

    public DateTime? DataEntrega { get; init; }

    public DateTime? DataCancelamento { get; init; }

    public PedidoEnderecoResponseDto? EnderecoEntrega { get; init; }

    public IReadOnlyList<PedidoItemResponseDto> Itens { get; init; } = [];

    public PedidoPagamentoResponseDto? Pagamento { get; init; }

    public PedidoEnvioResponseDto? Envio { get; init; }

    public IReadOnlyList<PedidoHistoricoResponseDto> Historico { get; init; } = [];
}

/// <summary>Linha da timeline de rastreio exibida ao cliente.</summary>
public sealed record RastreioEventoResponseDto
{
    public string Status { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public string? Local { get; init; }

    public DateTime OcorridoEm { get; init; }
}

public sealed record RastreioResponseDto
{
    public required string NumeroPedido { get; init; }

    public required string StatusEnvio { get; init; }

    public string? Transportadora { get; init; }

    public string? Servico { get; init; }

    public string? CodigoRastreio { get; init; }

    public IReadOnlyList<RastreioEventoResponseDto> Eventos { get; init; } = [];
}

/// <summary>Mudanca manual de status pelo painel de expedicao.</summary>
public sealed record AlterarStatusPedidoDto
{
    /// <summary>
    /// Nome do valor de StatusPedido. Texto e nao int de proposito: o front do painel manda o
    /// mesmo rotulo que exibe, e um int fora de faixa viraria um status inexistente no banco.
    /// </summary>
    [Required(ErrorMessage = "Informe o novo status.")]
    [StringLength(40)]
    public string StatusNovo { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Observacao { get; init; }
}

public sealed record CancelarPedidoDto
{
    [Required(ErrorMessage = "Informe o motivo do cancelamento.")]
    [StringLength(500, MinimumLength = 3)]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>
/// Filtro da listagem administrativa. E um contrato INTERNO entre controller e servico: o
/// controller recebe os parametros soltos na query string e monta este objeto. A paginacao
/// continua vindo em page/pageSize e sendo normalizada por PageRequest.
/// </summary>
public sealed record PedidoFiltroAdminDto
{
    /// <summary>Nome do valor de StatusPedido. Vazio traz todos.</summary>
    [StringLength(40)]
    public string? Status { get; init; }

    /// <summary>Casa por numero do pedido ou nome do destinatario.</summary>
    [StringLength(120)]
    public string? Busca { get; init; }

    public DateTime? De { get; init; }

    public DateTime? Ate { get; init; }
}
