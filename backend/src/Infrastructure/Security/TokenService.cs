using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Glorific.Application.Models.Auth;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Glorific.Infrastructure.Security;

/// <summary>
/// Emissao do access token JWT (HS256) e do refresh token opaco.
///
/// Quatro decisoes que existem por causa de bugs reais do repo de referencia:
/// 1. A chave sai de <see cref="JwtOptions.KeyEfetiva"/>, que ja aplica Trim. La o boot validava
///    com Trim e o emissor lia cru: uma env var com quebra de linha no fim passava na validacao
///    e invalidava a assinatura de TODOS os tokens em runtime.
/// 2. Todo "agora" vem de IClock. Um token de 8 h emitido com DateTime.Now num host UTC-3 valia
///    5 h, e ninguem entendia por que a sessao caia antes da hora.
/// 3. As claims usam os nomes CURTOS (sub, email, name, role). O handler tem um mapa que
///    reescreve nomes conhecidos para URIs longas; ele e limpo aqui para que o que o servidor
///    escreve seja exatamente o que o front le.
/// 4. O refresh token nao e JWT. Ele nunca precisa ser lido, so comparado — e ser opaco significa
///    que nao existe payload para alguem tentar interpretar ou forjar.
/// </summary>
public sealed class TokenService : ITokenService
{
    /// <summary>32 bytes = 256 bits de entropia. Menos que isso e adivinhavel em escala.</summary>
    private const int BytesRefreshToken = 32;

    private readonly JwtOptions _opcoes;
    private readonly IClock _relogio;
    private readonly SigningCredentials _credenciais;

    public TokenService(IOptions<JwtOptions> opcoes, IClock relogio)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _opcoes = opcoes.Value;
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));

        var chave = _opcoes.KeyEfetiva;

        // Falhar aqui derruba a primeira resolucao do servico, e nao a assinatura silenciosa de
        // um token fraco. HMAC-SHA256 com chave menor que o proprio bloco de hash e quebravel.
        if (Encoding.UTF8.GetByteCount(chave) < 32)
            throw new InvalidOperationException(
                "Jwt:Key precisa de ao menos 32 bytes para assinar em HS256. Defina Jwt__Key no ambiente.");

        _credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public AccessTokenGerado GerarAccessToken(Usuario usuario, IEnumerable<string> roles, Guid? idSessao = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var agora = _relogio.UtcNow;
        var expira = agora.AddMinutes(_opcoes.AccessTokenMinutos);

        var claims = new List<Claim>
        {
            // Identidade PUBLICA. O Id inteiro nunca vai para o token: ele e sequencial e
            // enumeravel, e um token e um objeto que circula fora do servidor.
            // Nomes curtos escritos como literal de proposito: sao o contrato entre o que o
            // servidor emite, o que o JwtBearer valida (NameClaimType/RoleClaimType) e o que o
            // front decodifica. Uma constante de biblioteca poderia mudar de valor numa
            // atualizacao e quebrar os tres lugares de uma vez.
            new("sub", usuario.Uuid),
            new("email", usuario.Email),

            // jti da a cada token uma identidade propria, que e o que permite rastrear um token
            // especifico num log de incidente.
            new("jti", Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(usuario.NomeCompleto))
            claims.Add(new Claim("name", usuario.NomeCompleto));

        // sid = familia do refresh, ou seja, a SESSAO. E o que liga um access token a cadeia de
        // rotacoes que o originou quando for preciso investigar.
        if (idSessao is not null)
            claims.Add(new Claim("sid", idSessao.Value.ToString()));

        // Papel vem SEMPRE do banco. Uma claim por papel: o JwtPayload agrupa repetidas num
        // array JSON, que e o formato que RequireRole entende dos dois lados.
        foreach (var papel in roles ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(papel))
                claims.Add(new Claim("role", papel));
        }

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Issuer,
            Audience = _opcoes.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = agora,
            Expires = expira,
            IssuedAt = agora,
            SigningCredentials = _credenciais
        };

        var handler = new JwtSecurityTokenHandler();

        // Sem isto o handler troca "sub" e "role" por URIs longas na saida, e o front acaba
        // lendo um nome de claim que nunca existe no token — foi exatamente o que aconteceu no
        // repo de referencia com decoded.nameidentifier.
        handler.OutboundClaimTypeMap.Clear();

        var token = handler.CreateEncodedJwt(descritor);

        return new AccessTokenGerado
        {
            Token = token,
            ExpiraEmUtc = expira,
            ExpiraEmSegundos = (int)Math.Max(0, Math.Round((expira - agora).TotalSeconds)),
            IdSessao = idSessao
        };
    }

    /// <inheritdoc />
    public RefreshTokenGerado GerarRefreshToken()
    {
        // RandomNumberGenerator e nao Random: o segundo e previsivel a partir da semente e um
        // refresh token adivinhavel e uma sessao de 30 dias de presente.
        var bytes = RandomNumberGenerator.GetBytes(BytesRefreshToken);

        // base64url: cabe em cookie e em URL sem escaping, ao contrario do base64 comum.
        var claro = Base64UrlEncoder.Encode(bytes);

        return new RefreshTokenGerado
        {
            TokenClaro = claro,
            TokenHash = HashRefreshToken(claro)
        };
    }

    /// <inheritdoc />
    public string HashRefreshToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        // Hex minusculo: 64 caracteres, exatamente o tamanho da coluna token_hash, e estavel
        // entre plataformas. Base64 traria "+" e "/", que ja causaram comparacao errada em URL.
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
