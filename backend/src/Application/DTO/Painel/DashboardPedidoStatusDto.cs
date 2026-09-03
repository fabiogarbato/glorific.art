using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Painel;

/// <summary>Uma barra do grafico "pedidos por status" do painel.</summary>
public sealed record DashboardPedidoStatusDto : ResponseDto
{
    public StatusPedido Status { get; init; }

    /// <summary>Nome do enum. O front tem o proprio mapa de rotulo, isto e so para log e CSV.</summary>
    public string StatusNome { get; init; } = string.Empty;

    public int Quantidade { get; init; }

    public int TotalCentavos { get; init; }
}
