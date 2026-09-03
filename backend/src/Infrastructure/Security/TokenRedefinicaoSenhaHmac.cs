using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Glorific.Infrastructure.Security;

/// <summary>
/// Token de redefinicao de senha autocontido: "uuid.expiracao.assinatura", tudo em base64url.
///
/// A assinatura e HMAC-SHA256 sobre uuid + expiracao + HASH DE SENHA ATUAL. Esse terceiro
/// ingrediente e o mecanismo inteiro: no instante em que a senha e trocada, o hash muda, e o
/// link que acabou de ser usado deixa de conferir. Uso unico sem tabela de tokens, sem worker de
/// limpeza e sem a janela de corrida classica entre "validei" e "marquei como usado".
///
/// A chave e DERIVADA da chave do JWT, nunca a chave crua: assinar coisas diferentes com o mesmo
/// segredo permite que um artefato de um contexto seja aceito no outro.
/// </summary>
public sealed class TokenRedefinicaoSenhaHmac : ITokenRedefinicaoSenha
{
    private const char Separador = '.';
    private const int PartesEsperadas = 3;

    private readonly byte[] _chave;

    public TokenRedefinicaoSenhaHmac(IOptions<JwtOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        // KeyEfetiva, e nao Key: a chave e lida com Trim no MESMO lugar em que e usada.
        var material = opcoes.Value.KeyEfetiva + "|glorific|redefinicao-senha|v1";

        _chave = SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    /// <inheritdoc />
    public string Gerar(string uuidUsuario, string? senhaHashAtual, DateTime expiraEmUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uuidUsuario);

        // SpecifyKind antes de converter: um DateTime com Kind Unspecified seria interpretado
        // como horario LOCAL, e o link nasceria com 3 horas a mais ou a menos de validade.
        var expiracao = new DateTimeOffset(DateTime.SpecifyKind(expiraEmUtc, DateTimeKind.Utc))
            .ToUnixTimeSeconds();

        var assinatura = Assinar(uuidUsuario, expiracao, senhaHashAtual);

        return string.Concat(
            Base64UrlEncoder.Encode(uuidUsuario),
            Separador,
            expiracao.ToString(CultureInfo.InvariantCulture),
            Separador,
            Base64UrlEncoder.Encode(assinatura));
    }

    /// <inheritdoc />
    public string? LerUuid(string token)
    {
        if (!TentarDividir(token, out var uuid, out _, out _))
            return null;

        return uuid;
    }

    /// <inheritdoc />
    public bool Validar(string token, string uuidUsuario, string? senhaHashAtual, DateTime agoraUtc)
    {
        if (!TentarDividir(token, out var uuid, out var expiracao, out var assinaturaRecebida))
            return false;

        // Comparacao ordinal: uuid e formato fixo e "casar sem diferenciar maiusculas" abriria
        // duas representacoes para a mesma conta.
        if (!string.Equals(uuid, uuidUsuario, StringComparison.Ordinal))
            return false;

        var agora = new DateTimeOffset(DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();

        if (expiracao <= agora)
            return false;

        var esperada = Assinar(uuidUsuario, expiracao, senhaHashAtual);

        // Tempo constante: comparar com == vaza, byte a byte, o quanto o palpite chegou perto.
        return assinaturaRecebida.Length == esperada.Length
            && CryptographicOperations.FixedTimeEquals(assinaturaRecebida, esperada);
    }

    private byte[] Assinar(string uuid, long expiracao, string? senhaHashAtual)
    {
        // Separador que nao aparece em nenhuma das partes: sem ele, "ab" + "c" e "a" + "bc"
        // produziriam a mesma assinatura e o token de um usuario serviria para outro.
        var conteudo = string.Join(
            '\n',
            uuid,
            expiracao.ToString(CultureInfo.InvariantCulture),
            senhaHashAtual ?? string.Empty);

        return HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(conteudo));
    }

    private static bool TentarDividir(
        string? token,
        out string uuid,
        out long expiracao,
        out byte[] assinatura)
    {
        uuid = string.Empty;
        expiracao = 0;
        assinatura = [];

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var partes = token.Split(Separador);

        if (partes.Length != PartesEsperadas)
            return false;

        if (!long.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out expiracao))
            return false;

        try
        {
            uuid = Base64UrlEncoder.Decode(partes[0]);
            assinatura = Base64UrlEncoder.DecodeBytes(partes[2]);
        }
        catch (Exception excecao) when (excecao is FormatException or ArgumentException)
        {
            // Token adulterado nao e falha de infraestrutura: sai como "link invalido".
            return false;
        }

        return uuid.Length > 0 && assinatura.Length > 0;
    }
}
