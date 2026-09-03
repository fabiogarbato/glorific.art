using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;

namespace Glorific.Domain.Entities.Promocoes;

/// <summary>
/// Ledger de uso do cupom. E o que permite "um por cliente" contando por IdUsuario, e o unico
/// (IdCupom, IdPedido) no banco e o que impede o mesmo pedido consumir o cupom duas vezes numa
/// retentativa de checkout.
///
/// ValorDescontado e gravado porque o calculo depende de regras que podem mudar depois
/// (teto, restricao de categoria) e o relatorio de investimento em promocao precisa do numero real.
/// </summary>
public class CupomUso : BaseEntity
{
    public int IdCupom { get; set; }
    public Cupom Cupom { get; set; } = null!;

    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int ValorDescontadoCentavos { get; set; }

    public DateTime DataUso { get; set; }
}
