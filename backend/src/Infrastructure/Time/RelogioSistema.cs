using Glorific.Domain.Interfaces;

namespace Glorific.Infrastructure.Time;

/// <summary>
/// Implementacao unica de IClock.
///
/// Regra dura do projeto: este e o UNICO lugar do codigo onde DateTime.UtcNow pode aparecer,
/// e DateTime.Now nao aparece em lugar nenhum. O bug de origem: token de 8 h emitido com
/// DateTime.Now num host UTC-3 valia 5 h. Centralizando aqui, o teste troca o relogio e
/// consegue exercitar expiracao de carrinho, backoff de envio e vigencia de cupom sem esperar.
/// </summary>
public sealed class RelogioSistema : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
