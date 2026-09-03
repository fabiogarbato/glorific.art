namespace Glorific.Application.Ports;

/// <summary>
/// Porta de envio de e-mail transacional (confirmacao de pedido, etiqueta postada, alerta ao
/// admin quando o envio esgota as tentativas).
///
/// Regra dura herdada de um incidente real: e-mail NUNCA e enviado dentro da transacao do banco.
/// No repo de referencia o SMTP era chamado dentro da transacao do pagamento — com o servidor de
/// e-mail fora do ar, um pagamento ja confirmado sofria rollback. Aqui o envio acontece depois
/// do commit, e falhar so gera log.
/// </summary>
public interface IEmailSender
{
    /// <param name="destinatario">Um endereco. Envio em massa nao e responsabilidade desta porta.</param>
    /// <param name="assunto">Sem quebra de linha — cabecalho de e-mail nao aceita.</param>
    /// <param name="corpoHtml">HTML ja renderizado.</param>
    Task EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken ct = default);
}
