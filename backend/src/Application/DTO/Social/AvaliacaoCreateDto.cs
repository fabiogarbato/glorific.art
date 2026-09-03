using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Social;

/// <summary>
/// Avaliacao enviada pelo cliente.
///
/// Os campos de caimento (TamanhoComprado, AlturaClienteCm, PesoClienteKg, Caimento) sao o motivo
/// principal de existir avaliacao em loja de roupa: "veste pequeno, peguei um numero acima" e o
/// que reduz devolucao. Sao opcionais porque exigi-los derruba a taxa de envio de review, mas a
/// listagem publica os expoe sempre que existirem.
///
/// IdPedidoItem e opcional: quando o front nao souber informar, o servico procura a compra do
/// proprio usuario para aquele produto. O que NAO e opcional e ter comprado.
/// </summary>
public sealed record AvaliacaoCreateDto : CreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Produto invalido.")]
    public int IdProduto { get; init; }

    /// <summary>Sustenta o selo de compra verificada. Quando ausente, o servico resolve sozinho.</summary>
    public int? IdPedidoItem { get; init; }

    [Range(1, 5, ErrorMessage = "A nota deve ser de 1 a 5.")]
    public int Nota { get; init; }

    [StringLength(120)]
    public string? Titulo { get; init; }

    [StringLength(4000)]
    public string? Comentario { get; init; }

    [StringLength(20)]
    public string? TamanhoComprado { get; init; }

    [Range(80, 250, ErrorMessage = "Altura em centimetros fora da faixa aceita.")]
    public int? AlturaClienteCm { get; init; }

    [Range(20, 300, ErrorMessage = "Peso em quilos fora da faixa aceita.")]
    public decimal? PesoClienteKg { get; init; }

    [EnumDataType(typeof(CaimentoTamanho), ErrorMessage = "Caimento invalido.")]
    public CaimentoTamanho? Caimento { get; init; }

    public bool? Recomenda { get; init; }

    /// <summary>Ids de midias ja enviadas ao storage. Entram na ordem em que chegam.</summary>
    public IReadOnlyList<int> IdsMidia { get; init; } = [];
}
