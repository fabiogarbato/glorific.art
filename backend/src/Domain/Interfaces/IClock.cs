namespace Glorific.Domain.Interfaces;

/// <summary>
/// Fonte unica de "agora". Regra dura do projeto: ZERO DateTime.Now no codigo.
///
/// O bug real que motiva isso: no repo de referencia o token de 8 h emitido com DateTime.Now
/// num host UTC-3 valia 5 h. Alem disso, sem esta abstracao nao da para testar expiracao de
/// carrinho, backoff de envio ou vigencia de cupom sem esperar o relogio andar.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
