using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Alteracao de cupom pelo painel. Sem Id: ele vem da rota.
///
/// UsosAtuais NAO aparece aqui de proposito. O contador e escrito por UPDATE condicional atomico
/// no repositorio; deixar o painel sobrescrever esse numero num PUT reabriria exatamente a corrida
/// que o UPDATE condicional existe para fechar.
/// </summary>
public sealed record CupomUpdateDto : UpdateDto
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

    [Range(0, 100_000_000, ErrorMessage = "Valor do cupom fora da faixa aceita.")]
    public int Valor { get; init; }

    [Range(0, int.MaxValue)]
    public int? ValorMinimoPedidoCentavos { get; init; }

    [Range(1, int.MaxValue)]
    public int? DescontoMaximoCentavos { get; init; }

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
