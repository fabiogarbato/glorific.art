using Glorific.Domain.Common;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Pedidos;

/// <summary>
/// Historico de rastreio. OcorridoEm e o instante informado pela transportadora e RegistradoEm
/// e quando nos soubemos: os dois divergem em horas, e a timeline do cliente precisa mostrar o
/// primeiro enquanto o suporte investiga com o segundo.
/// </summary>
public class EnvioEvento : BaseEntity
{
    public int IdEnvio { get; set; }
    public Envio Envio { get; set; } = null!;

    public StatusEnvio Status { get; set; }

    public string? Descricao { get; set; }
    public string? Local { get; set; }

    public DateTime OcorridoEm { get; set; }
    public DateTime RegistradoEm { get; set; }
}
