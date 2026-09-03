using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Promocoes;

/// <summary>
/// Promocao real do sistema — PrecoComparativo do produto e so o "de/por" riscado, sem vigencia.
///
/// FreteGratis e tipo proprio, e nao um percentual de 100 por cento sobre o frete, porque o custo
/// real continua sendo pago ao Melhor Envio: o desconto entra na linha de frete do pedido,
/// enquanto Envio.ValorCompradoCentavos segue registrando o que saiu da carteira.
///
/// DescontoMaximo existe para "50 por cento OFF" em pedido de dois mil reais nao virar prejuizo.
/// UsosAtuais e incrementado por UPDATE condicional, nunca por leitura seguida de escrita:
/// dois checkouts simultaneos consomem o ultimo uso do cupom "primeiros 100".
/// </summary>
public class Cupom : BaseEntity, IAuditable
{
    /// <summary>Sempre maiusculo. O usuario digita como quiser, a normalizacao e nossa.</summary>
    public required string Codigo { get; set; }

    public string? Descricao { get; set; }

    public TipoCupom Tipo { get; set; } = TipoCupom.Percentual;

    /// <summary>Percentual multiplicado por 100 (1250 = 12,50 por cento) ou centavos, conforme o Tipo.</summary>
    public int Valor { get; set; }

    public int? ValorMinimoPedidoCentavos { get; set; }

    /// <summary>Teto do desconto percentual, em centavos.</summary>
    public int? DescontoMaximoCentavos { get; set; }

    /// <summary>Null significa ilimitado.</summary>
    public int? UsoMaximoTotal { get; set; }

    public int UsoMaximoPorUsuario { get; set; } = 1;

    public int UsosAtuais { get; set; }

    public DateTime VigenciaInicio { get; set; }
    public DateTime? VigenciaFim { get; set; }

    public bool PrimeiraCompraApenas { get; set; }

    public int? IdCategoriaRestrita { get; set; }
    public Categoria? CategoriaRestrita { get; set; }

    public int? IdColecaoRestrita { get; set; }
    public Colecao? ColecaoRestrita { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<CupomUso> Usos { get; set; } = [];
    public ICollection<Pedido> Pedidos { get; set; } = [];
}
