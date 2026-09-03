using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// O pedido e um documento fechado: todo valor cobrado esta gravado aqui, nenhum e recalculado
/// na leitura. Recalcular significaria que uma mudanca de tabela de preco ou de regra de cupom
/// reescreve recibo antigo.
///
/// Numero e o identificador humano (GA-2026-000137) que aparece no e-mail e no suporte;
/// Uuid e o identificador publico da URL. O Id inteiro nunca sai para o front.
///
/// O snapshot de frete (transportadora, servico, prazo) fica no pedido e nao so no envio porque
/// o cliente ve "Sedex, 4 dias uteis" na confirmacao antes de existir qualquer etiqueta.
/// </summary>
public class Pedido : BaseEntity
{
    public required string Numero { get; set; }
    public required string Uuid { get; set; }

    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public StatusPedido Status { get; set; } = StatusPedido.AguardandoPagamento;

    /// <summary>Soma das linhas, em centavos, antes de cupom e frete.</summary>
    public int SubtotalCentavos { get; set; }

    public int DescontoCupomCentavos { get; set; }

    /// <summary>Frete COBRADO do cliente, em centavos. O custo real pago ao Melhor Envio fica em Envio.</summary>
    public int FreteCentavos { get; set; }

    public int TotalCentavos { get; set; }

    public int? IdCupom { get; set; }
    public Cupom? Cupom { get; set; }

    /// <summary>O codigo como estava no dia. O cupom pode ser renomeado ou apagado depois.</summary>
    public string? CodigoCupomSnapshot { get; set; }

    public int? IdServicoFrete { get; set; }
    public string? TransportadoraFrete { get; set; }
    public string? ServicoFrete { get; set; }
    public int? PrazoFreteDias { get; set; }

    public PedidoEnderecoSnapshot EnderecoEntrega { get; set; } = null!;

    public string? ObservacaoCliente { get; set; }

    /// <summary>Peso usado na cotacao. Congelado para a cotacao continuar explicavel depois.</summary>
    public int PesoTotalGramas { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataPagamento { get; set; }
    public DateTime? DataEnvio { get; set; }
    public DateTime? DataEntrega { get; set; }
    public DateTime? DataCancelamento { get; set; }
    public string? MotivoCancelamento { get; set; }

    public ICollection<PedidoItem> Itens { get; set; } = [];
    public ICollection<PedidoHistorico> Historico { get; set; } = [];

    /// <summary>Um pagamento por pedido, garantido por indice unico no banco.</summary>
    public Pagamento? Pagamento { get; set; }

    /// <summary>Um envio por pedido: a unicidade no banco e o que impede etiqueta duplicada.</summary>
    public Envio? Envio { get; set; }

    public ICollection<CupomUso> CupomUsos { get; set; } = [];
}
