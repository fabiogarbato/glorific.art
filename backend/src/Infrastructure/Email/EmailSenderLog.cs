using Glorific.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Glorific.Infrastructure.Email;

/// <summary>
/// IMPLEMENTACAO DE BORDA, registrada com TryAdd: existe apenas para que a ausencia do adaptador
/// SMTP real nao derrube a resolucao inteira do AuthController por falta de IEmailSender.
///
/// Assim que o adaptador de verdade (MailKit) for registrado, ele ganha e esta classe some do
/// caminho. Enquanto isso, um pedido de "esqueci minha senha" grava um aviso no log com o
/// destinatario e o assunto — e nao um sucesso silencioso, que seria pior: o cliente esperando um
/// e-mail que ninguem enviou e ninguem sabendo disso.
/// </summary>
public sealed class EmailSenderLog : IEmailSender
{
    private readonly ILogger<EmailSenderLog> _logger;

    public EmailSenderLog(ILogger<EmailSenderLog> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        // O corpo NAO entra no log: ele carrega o link de redefinicao de senha, e log e um lugar
        // que muita gente le e que costuma ser exportado para fora.
        _logger.LogWarning(
            "Nenhum adaptador de e-mail configurado. Mensagem NAO enviada para {Destinatario}. Assunto: {Assunto}.",
            destinatario,
            assunto);

        return Task.CompletedTask;
    }
}
