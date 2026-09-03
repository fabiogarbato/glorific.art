using Glorific.Domain.Common;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// A entidade e simultaneamente agregado e FILA do worker de etiquetas: o worker busca por
/// Status mais ProximaTentativaEm, e por isso os campos de retentativa moram aqui e nao numa
/// tabela de jobs separada.
///
/// IdPedido e unico no banco — e essa unicidade, e nao um if no codigo, que garante que nao se
/// compra duas etiquetas para o mesmo pedido.
///
/// ValorCotado e o que foi mostrado ao cliente; ValorComprado e o custo real debitado da carteira
/// do Melhor Envio. Os dois separados sao o que permite medir margem de frete, inclusive quando
/// um cupom de frete gratis zerou a cobranca mas o custo continuou existindo.
/// </summary>
public class Envio : BaseEntity
{
    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    /// <summary>Uuid da etiqueta no Melhor Envio. Null enquanto nada foi comprado la.</summary>
    public string? MeOrderId { get; set; }

    public int IdServico { get; set; }
    public string? NomeServico { get; set; }
    public string? NomeTransportadora { get; set; }

    public int ValorCotadoCentavos { get; set; }
    public int? ValorCompradoCentavos { get; set; }

    public StatusEnvio Status { get; set; } = StatusEnvio.Pendente;

    public string? CodigoRastreio { get; set; }
    public string? UrlEtiqueta { get; set; }

    /// <summary>Chave da nota fiscal, exigida por algumas transportadoras antes da postagem.</summary>
    public string? ChaveNfe { get; set; }

    public int Tentativas { get; set; }

    /// <summary>Truncado em 2000 caracteres: stack trace do parceiro nao pode estourar a linha.</summary>
    public string? UltimoErro { get; set; }

    /// <summary>Backoff exponencial calculado por EnvioRetryPolicy.</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    public string? RawUltimaResposta { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<EnvioEvento> Eventos { get; set; } = [];
}
