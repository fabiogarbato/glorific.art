using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.Ports.Options;

/// <summary>
/// Secao "Google". Configuracao do login com Google Identity Services.
/// </summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    /// <summary>
    /// Client ID OAuth do projeto. E a AUDIENCE esperada do id_token: sem conferir aud, um token
    /// emitido para outro aplicativo qualquer seria aceito como login valido aqui.
    /// O mesmo valor vai no VITE_GOOGLE_CLIENT_ID do front.
    /// </summary>
    [Required(ErrorMessage = "Google:ClientId e obrigatorio.")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Nao e necessario no fluxo de id_token (o front usa GSI e o back so valida assinatura).
    /// Fica declarado para o dia em que houver troca de authorization code server-side.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>Tolerancia de relogio em iat/exp na validacao do id_token.</summary>
    [Range(0, 300, ErrorMessage = "Google:ToleranciaRelogioSegundos deve estar entre 0 e 300.")]
    public int ToleranciaRelogioSegundos { get; set; } = 30;

    /// <summary>
    /// Dominios permitidos (claim hd). Vazio = qualquer conta Google, que e o correto numa loja
    /// aberta ao publico. So faz sentido preencher em ambiente interno.
    /// </summary>
    public IList<string> DominiosPermitidos { get; set; } = [];

    /// <summary>
    /// Sufixo de TODO client id OAuth emitido pelo Google.
    ///
    /// Serve de sanidade: um valor que nao termina assim nao e um client id — e o placeholder do
    /// appsettings, um texto de "preencha aqui" ou uma variavel de ambiente trocada. Sem esta
    /// conferencia, qualquer um deles vira uma audience que nenhum id_token real vai casar, e a
    /// loja passa a responder "login invalido" para TODO mundo enquanto o problema real e uma
    /// configuracao em branco.
    /// </summary>
    public const string SufixoClientIdGoogle = ".apps.googleusercontent.com";

    /// <summary>
    /// True quando o login com Google nao tem como funcionar nesta instalacao: ClientId em
    /// branco, ainda com o placeholder versionado, ou que nao e um client id do Google.
    ///
    /// A checagem e feita em RUNTIME, e nao so no boot, porque o fail-fast do boot
    /// (RequiredSecret) so roda fora de Development — e e justamente em Development que alguem
    /// abre a loja com a variavel vazia e precisa entender o motivo.
    /// </summary>
    public bool NaoConfigurado =>
        SegredoPlaceholder.NaoConfigurado(ClientId) ||
        !ClientId.Trim().EndsWith(SufixoClientIdGoogle, StringComparison.OrdinalIgnoreCase);
}
