using System.Collections.Concurrent;
using Glorific.Application.Models.Auth;
using Glorific.Application.Ports;

namespace Glorific.Tests.TestSupport;

/// <summary>
/// Dublê da porta <see cref="IGoogleTokenValidator"/>.
///
/// POR QUE ELE PRECISA EXISTIR: um id_token que passe pela validacao de verdade so pode ser
/// produzido com a chave privada do Google. Sem o dublê, TUDO no login com Google morre no 401 e
/// nada do que vem depois — casamento por e-mail verificado, conta desativada, criacao de conta
/// sem senha, papel cliente — chega a ser exercitado uma unica vez. Foi exatamente esse o estado
/// do fluxo ate agora: escrito, revisado e nunca executado.
///
/// O QUE ELE NAO FAZ: nao confere assinatura, issuer nem audience. Essas tres sao do adaptador
/// de verdade e estao cobertas, offline, em GoogleTokenValidatorTests. A fronteira aqui e outra:
/// "o Google disse que esta pessoa e quem diz ser; e agora?".
///
/// A resposta e indexada pelo proprio id_token, para que UM host de teste atenda todos os
/// cenarios da suite — subir um host por cenario reaplicaria migration e seed a cada vez.
///
/// Token desconhecido devolve null, que e exatamente o que o adaptador de verdade faz com token
/// invalido, expirado ou de outra audience. Ou seja: o caso (g) sai de graca, sem registro.
/// </summary>
public sealed class GoogleDeTeste : IGoogleTokenValidator
{
    private readonly ConcurrentDictionary<string, GoogleIdentityInfo> _porToken = new(StringComparer.Ordinal);

    /// <summary>
    /// Registra uma identidade e devolve o id_token que o teste deve enviar para receber ela.
    /// </summary>
    public string Registrar(GoogleIdentityInfo identidade)
    {
        var idToken = $"id-token-de-teste-{Guid.NewGuid():N}";
        _porToken[idToken] = identidade;

        return idToken;
    }

    /// <summary>
    /// Atalho do caso comum: conta Google com e-mail verificado.
    ///
    /// O <c>sub</c> e aleatorio por padrao — dois testes que compartilhassem o mesmo sub
    /// disputariam a mesma linha de logins_externos e passariam a depender da ordem de execucao.
    /// </summary>
    public string RegistrarConta(
        string email,
        bool emailVerificado = true,
        string? subject = null,
        string? nome = "Maria Souza",
        string? fotoUrl = "https://exemplo.test/foto.png") =>
        Registrar(new GoogleIdentityInfo
        {
            Subject = subject ?? $"sub-google-{Guid.NewGuid():N}",
            Email = email,
            EmailVerificado = emailVerificado,
            Nome = nome,
            FotoUrl = fotoUrl
        });

    public Task<GoogleIdentityInfo?> ValidarAsync(string idToken, CancellationToken ct = default) =>
        Task.FromResult(_porToken.TryGetValue(idToken ?? string.Empty, out var identidade) ? identidade : null);
}
