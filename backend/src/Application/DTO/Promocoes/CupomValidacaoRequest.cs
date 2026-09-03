namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Entrada da validacao de cupom. E um contrato INTERNO entre casos de uso (carrinho e checkout
/// chamam o CupomService), nao um corpo de requisicao HTTP — por isso nao herda CreateDto nem
/// carrega DataAnnotations.
///
/// FreteCentavos entra porque o tipo FreteGratis desconta a linha de frete, e nao o subtotal:
/// o custo real continua sendo pago ao Melhor Envio e registrado em Envio.ValorCompradoCentavos.
/// </summary>
public sealed record CupomValidacaoRequest
{
    public string Codigo { get; init; } = string.Empty;

    public int IdUsuario { get; init; }

    /// <summary>Soma das linhas antes de cupom e frete, em centavos.</summary>
    public int SubtotalCentavos { get; init; }

    /// <summary>Frete cotado e cobrado do cliente, em centavos.</summary>
    public int FreteCentavos { get; init; }

    /// <summary>Obrigatorio quando o cupom pode ter restricao de categoria ou colecao.</summary>
    public IReadOnlyList<CupomItemContexto> Itens { get; init; } = [];
}
