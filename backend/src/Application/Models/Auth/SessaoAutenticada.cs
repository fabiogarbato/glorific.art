using Glorific.Application.DTO.Identidade;

namespace Glorific.Application.Models.Auth;

/// <summary>
/// Resultado completo de um login, cadastro ou renovacao — a forma como o SERVICO devolve, que
/// nao e a forma como a API responde.
///
/// A diferenca e o ponto: <see cref="RefreshTokenClaro"/> existe aqui e nao existe no
/// AutenticacaoResponseDto. O controller pega este campo, escreve o cookie httpOnly e joga o
/// resto no corpo. Assim o unico caminho pelo qual o refresh token sai do servidor e o cookie,
/// e nao ha como alguem serializar este record por engano numa resposta.
/// </summary>
public sealed record SessaoAutenticada
{
    public required string AccessToken { get; init; }

    /// <summary>Segundos ate o access token expirar.</summary>
    public required int ExpiraEmSegundos { get; init; }

    /// <summary>
    /// Refresh token opaco em CLARO. Sai daqui direto para o cookie httpOnly e nunca e
    /// persistido: o banco guarda apenas o SHA-256.
    /// </summary>
    public required string RefreshTokenClaro { get; init; }

    /// <summary>Vira o Expires do cookie. Cookie e linha do banco expiram no mesmo instante.</summary>
    public required DateTime RefreshTokenExpiraEmUtc { get; init; }

    /// <summary>Familia da rotacao, ou seja, a sessao. E a claim sid do access token.</summary>
    public required Guid IdSessao { get; init; }

    public required UsuarioResponseDto Usuario { get; init; }
}

/// <summary>
/// Quem originou a requisicao, para a auditoria da linha de refresh_tokens.
///
/// Nao e seguranca — IP e User-Agent sao forjaveis. E investigacao: quando um reuso de token
/// dispara a revogacao da familia, estes dois campos sao a unica forma de responder "de onde
/// veio a segunda apresentacao".
/// </summary>
public sealed record OrigemRequisicao
{
    public static readonly OrigemRequisicao Desconhecida = new();

    /// <summary>IPv4 ou IPv6 ja resolvido pelo ForwardedHeaders. Coluna aceita 45 caracteres.</summary>
    public string? Ip { get; init; }

    /// <summary>Coluna aceita 400 caracteres; o servico trunca antes de gravar.</summary>
    public string? UserAgent { get; init; }
}
