using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Promocoes;

/// <summary>
/// Resultado de um cupom aceito.
///
/// Os dois descontos sao SEPARADOS de proposito. DescontoProdutosCentavos vai para
/// Pedido.DescontoCupomCentavos; DescontoFreteCentavos zera a linha de frete cobrada do cliente
/// (Pedido.FreteCentavos), sem tocar em Envio.ValorCompradoCentavos, que continua registrando o
/// que efetivamente saiu da carteira do Melhor Envio. Juntar os dois num numero so tornaria
/// impossivel medir a margem de frete depois.
///
/// BaseElegivelCentavos e gravado para o suporte conseguir explicar o valor: em cupom restrito a
/// categoria, o percentual nao incide sobre o carrinho inteiro.
/// </summary>
public sealed record CupomAplicadoDto : ResponseDto
{
    public int IdCupom { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public TipoCupom Tipo { get; init; }

    /// <summary>Desconto sobre os produtos, em centavos.</summary>
    public int DescontoProdutosCentavos { get; init; }

    /// <summary>Quanto sai da linha de frete cobrada do cliente, em centavos.</summary>
    public int DescontoFreteCentavos { get; init; }

    public bool FreteGratis { get; init; }

    /// <summary>Soma das linhas sobre a qual o desconto incidiu, em centavos.</summary>
    public int BaseElegivelCentavos { get; init; }

    /// <summary>Soma dos dois descontos. Conveniencia de exibicao, nunca de contabilidade.</summary>
    public int DescontoTotalCentavos => DescontoProdutosCentavos + DescontoFreteCentavos;
}
