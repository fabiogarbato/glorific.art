using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// Idempotencia de webhook feita no BANCO, nao no codigo.
///
/// O repo de referencia protegia com um "if (status == Completed) return" dentro do handler:
/// funciona para o caso feliz, mas nao impede processar duas vezes eventos de tipos diferentes
/// fora de ordem e nao deixa rastro do que chegou. Aqui o evento e gravado PRIMEIRO com
/// ProviderEventId unico: a reentrega vira violacao de unicidade, traduzida em 200 imediato.
///
/// O webhook grava e responde rapido; o processamento pesado roda em worker. Sem isso o envio
/// de e-mail acaba dentro da transacao do pagamento e um SMTP fora do ar derruba pagamento ja
/// confirmado.
/// </summary>
public class PagamentoEvento : BaseEntity
{
    /// <summary>Nullable porque o evento pode chegar antes de sabermos a qual pagamento pertence.</summary>
    public int? IdPagamento { get; set; }
    public Pagamento? Pagamento { get; set; }

    public required string ProviderEventId { get; set; }

    public required string Tipo { get; set; }

    /// <summary>Payload cru em jsonb, guardado exatamente como chegou.</summary>
    public required string Payload { get; set; }

    public DateTime RecebidoEm { get; set; }

    /// <summary>Null enquanto na fila do worker.</summary>
    public DateTime? ProcessadoEm { get; set; }

    public string? Erro { get; set; }
}
