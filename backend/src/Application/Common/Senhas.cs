using System.Text;
using Glorific.Application.Exceptions;

namespace Glorific.Application.Common;

/// <summary>
/// Politica unica de hash de senha do sistema. BCrypt com fator de trabalho explicito.
///
/// Existe como classe propria — e nao como duas linhas dentro do servico de autenticacao —
/// porque tres lugares precisam do MESMO algoritmo e do MESMO fator: cadastro, redefinicao de
/// senha e o seeder do admin inicial. Fator diferente entre eles nao quebra nada visivelmente
/// (o hash carrega o proprio custo), so faz metade das senhas do banco valerem menos.
/// </summary>
public static class Senhas
{
    /// <summary>
    /// Fator 12 = 4096 iteracoes. Custa ~250 ms num servidor comum, que e caro o suficiente
    /// para forca bruta offline e barato o suficiente para um login interativo.
    /// </summary>
    public const int FatorTrabalho = 12;

    /// <summary>Limite do proprio BCrypt: ele IGNORA silenciosamente o que passa de 72 bytes.</summary>
    public const int MaximoBytes = 72;

    /// <summary>
    /// Hash descartavel usado so para igualar o tempo de resposta quando o e-mail nao existe.
    /// Lazy para pagar o custo do calculo uma vez por processo, e nao a cada login falho.
    /// </summary>
    private static readonly Lazy<string> HashDescarte =
        new(() => Hash(Guid.NewGuid().ToString("N")), LazyThreadSafetyMode.ExecutionAndPublication);

    public static string Hash(string senha)
    {
        GarantirTamanho(senha);
        return BCrypt.Net.BCrypt.HashPassword(senha, FatorTrabalho);
    }

    /// <summary>Falso quando o hash e nulo/vazio — conta de Google nunca definiu senha.</summary>
    public static bool Confere(string? senha, string? hash)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrompido no banco nao pode virar 500: para o cliente e credencial invalida.
            return false;
        }
    }

    /// <summary>
    /// Queima o mesmo tempo de um Verify real.
    ///
    /// Sem isto, "e-mail inexistente" responde em 1 ms e "senha errada" em 250 ms — a diferenca
    /// e mensuravel de fora e transforma o login num oraculo de quais e-mails tem conta aqui.
    /// </summary>
    public static void Equalizar(string? senha)
    {
        _ = Confere(string.IsNullOrEmpty(senha) ? "-" : senha, HashDescarte.Value);
    }

    private static void GarantirTamanho(string senha)
    {
        BusinessValidationException.LancarSeVazio(senha, "A senha e obrigatoria.");

        // Truncar em silencio significaria que "senha de 80 caracteres" e "os 72 primeiros
        // caracteres dela" abrem a mesma conta. Melhor recusar do que enfraquecer sem avisar.
        if (Encoding.UTF8.GetByteCount(senha) > MaximoBytes)
            throw new BusinessValidationException($"A senha nao pode passar de {MaximoBytes} bytes.");
    }
}
