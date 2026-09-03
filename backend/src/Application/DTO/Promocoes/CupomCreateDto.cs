using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Criacao de cupom pelo painel.
///
/// Valor e polimorfico por Tipo: percentual multiplicado por 100 (1250 = 12,50 por cento) ou
/// centavos. DataAnnotation nao consegue expressar isso sozinha — a coerencia entre Tipo e Valor
/// e validada no servico, onde a mensagem de erro pode dizer qual das duas leituras se aplica.
/// </summary>
public sealed record CupomCreateDto : CreateDto
{
    [Required(ErrorMessage = "Informe o codigo do cupom.")]
    [StringLength(40, MinimumLength = 3, ErrorMessage = "O codigo deve ter de 3 a 40 caracteres.")]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "O codigo aceita apenas letras, numeros, ponto, hifen e sublinhado.")]
    public string Codigo { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Descricao { get; init; }

    [Required]
    [EnumDataType(typeof(TipoCupom), ErrorMessage = "Tipo de cupom invalido.")]
    public TipoCupom Tipo { get; init; } = TipoCupom.Percentual;

    /// <summary>Percentual x100 quando Tipo=Percentual; centavos quando Tipo=ValorFixo; ignorado em FreteGratis.</summary>
    [Range(0, 100_000_000, ErrorMessage = "Valor do cupom fora da faixa aceita.")]
    public int Valor { get; init; }

    [Range(0, int.MaxValue)]
    public int? ValorMinimoPedidoCentavos { get; init; }

    /// <summary>Teto do desconto percentual, em centavos. E o que evita "50 por cento OFF" virar prejuizo.</summary>
    [Range(1, int.MaxValue)]
    public int? DescontoMaximoCentavos { get; init; }

    /// <summary>Null significa ilimitado.</summary>
    [Range(1, int.MaxValue)]
    public int? UsoMaximoTotal { get; init; }

    [Range(1, int.MaxValue)]
    public int UsoMaximoPorUsuario { get; init; } = 1;

    [Required]
    public DateTime VigenciaInicio { get; init; }

    public DateTime? VigenciaFim { get; init; }

    public bool PrimeiraCompraApenas { get; init; }

    public int? IdCategoriaRestrita { get; init; }

    public int? IdColecaoRestrita { get; init; }

    public bool Ativo { get; init; } = true;
}
