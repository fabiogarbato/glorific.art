namespace Glorific.Application.Ports;

/// <summary>
/// Porta do token de redefinicao de senha ("esqueci minha senha").
///
/// POR QUE ELE NAO E UMA LINHA DE TABELA: o token e AUTOCONTIDO e assinado, e a assinatura
/// inclui o hash de senha ATUAL do usuario. Duas consequencias caem de graca:
/// 1. USO UNICO de verdade — no instante em que a senha muda, o hash muda, e a assinatura do
///    token que acabou de ser usado deixa de conferir. Nao existe janela em que o mesmo link
///    funcione duas vezes, e nao existe linha esquecida no banco para um worker limpar.
/// 2. Nenhuma migration nova, nenhuma tabela a mais, nenhum estado a sincronizar.
///
/// O preco: nao da para revogar um link individual antes da hora sem trocar a senha. Aceitavel
/// para uma janela de 30 minutos.
///
/// A implementacao usa HMAC-SHA256 com chave DERIVADA da chave do JWT — nunca a chave crua, para
/// que um artefato assinado aqui nao possa ser confundido com um token de acesso em lugar nenhum.
/// </summary>
public interface ITokenRedefinicaoSenha
{
    /// <param name="senhaHashAtual">
    /// Hash de senha do usuario NESTE momento. Null para conta que so tem Google — e o que
    /// permite que ela defina a primeira senha por este mesmo fluxo.
    /// </param>
    string Gerar(string uuidUsuario, string? senhaHashAtual, DateTime expiraEmUtc);

    /// <summary>
    /// Le o uuid do usuario SEM validar assinatura, so para localizar a conta e obter o hash
    /// atual que a validacao exige. Nada pode ser decidido a partir deste valor sozinho.
    /// </summary>
    /// <returns>O uuid, ou null se o token estiver malformado.</returns>
    string? LerUuid(string token);

    /// <summary>
    /// Confere assinatura e expiracao em tempo constante.
    /// </summary>
    /// <returns>Falso para token adulterado, expirado, de outro usuario ou ja utilizado.</returns>
    bool Validar(string token, string uuidUsuario, string? senhaHashAtual, DateTime agoraUtc);
}
