using System.Text;
using Glorific.Application.Common;
using Glorific.Application.Exceptions;
using Xunit;

namespace Glorific.Tests.Seguranca;

/// <summary>
/// Politica de hash de senha. BCrypt com fator 12 custa ~250 ms por chamada, entao os testes
/// que precisam de um hash real reaproveitam UM hash calculado uma vez por processo — a suite
/// continua rapida sem abrir mao de exercitar o algoritmo de verdade.
/// </summary>
public class SenhasTests
{
    private const string SenhaConhecida = "Gl0rific!2026";

    private static readonly Lazy<string> HashDaSenhaConhecida =
        new(() => Senhas.Hash(SenhaConhecida), LazyThreadSafetyMode.ExecutionAndPublication);

    // ---------------------------------------------------------------- constantes da politica

    [Fact]
    public void FatorTrabalho_ValorDaPolitica_EhDoze()
    {
        Assert.Equal(12, Senhas.FatorTrabalho);
    }

    [Fact]
    public void MaximoBytes_ValorDaPolitica_EhSetentaEDois()
    {
        Assert.Equal(72, Senhas.MaximoBytes);
    }

    // ---------------------------------------------------------------- Hash

    /// <summary>
    /// Salt aleatorio por chamada: duas contas com a MESMA senha nao podem ter o mesmo hash,
    /// senao um vazamento do banco entrega de graca quais usuarios repetem senha.
    /// </summary>
    [Fact]
    public void Hash_ChamadoDuasVezesComAMesmaSenha_ProduzHashesDiferentes()
    {
        var primeiro = Senhas.Hash(SenhaConhecida);
        var segundo = Senhas.Hash(SenhaConhecida);

        Assert.NotEqual(primeiro, segundo);
        Assert.True(Senhas.Confere(SenhaConhecida, primeiro));
        Assert.True(Senhas.Confere(SenhaConhecida, segundo));
    }

    /// <summary>O fator de trabalho tem que estar gravado no proprio hash, senao ele nao vale nada.</summary>
    [Fact]
    public void Hash_QualquerSenha_GravaOFatorDeTrabalhoNoProprioHash()
    {
        var hash = HashDaSenhaConhecida.Value;

        Assert.StartsWith("$2", hash);
        Assert.Contains($"${Senhas.FatorTrabalho}$", hash);
        Assert.Equal(60, hash.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Hash_SenhaVaziaOuSoEspaco_LancaBusinessValidationException(string senha)
    {
        Assert.Throws<BusinessValidationException>(() => Senhas.Hash(senha));
    }

    [Fact]
    public void Hash_SenhaNula_LancaBusinessValidationException()
    {
        Assert.Throws<BusinessValidationException>(() => Senhas.Hash(null!));
    }

    // ---------------------------------------------------------------- limite de 72 bytes

    /// <summary>Exatamente no limite: 72 bytes ASCII ainda e aceito.</summary>
    [Fact]
    public void Hash_SenhaComExatamenteSetentaEDoisBytes_EhAceita()
    {
        var senha = new string('a', 72);
        Assert.Equal(72, Encoding.UTF8.GetByteCount(senha));

        var hash = Senhas.Hash(senha);

        Assert.True(Senhas.Confere(senha, hash));
    }

    /// <summary>
    /// Um byte acima do limite ja e recusado. O BCrypt truncaria em silencio, e ai
    /// "senha de 73 caracteres" e "os 72 primeiros dela" abririam a MESMA conta.
    /// Recusar e a unica saida honesta.
    /// </summary>
    [Fact]
    public void Hash_SenhaComSetentaETresBytes_LancaBusinessValidationException()
    {
        var senha = new string('a', 73);

        var erro = Assert.Throws<BusinessValidationException>(() => Senhas.Hash(senha));

        Assert.Contains("72", erro.Message);
    }

    /// <summary>
    /// O limite e de BYTES, nao de caracteres: 36 letras acentuadas ja ocupam 72 bytes em UTF-8.
    /// Contar caracteres deixaria passar senha que o BCrypt truncaria.
    /// </summary>
    [Fact]
    public void Hash_SenhaComAcentoNoLimiteDeBytes_ContaBytesENaoCaracteres()
    {
        var noLimite = new string('á', 36);      // 36 letras "a" acentuadas = 72 bytes
        var acimaDoLimite = new string('á', 37); // 37 letras = 74 bytes

        Assert.Equal(72, Encoding.UTF8.GetByteCount(noLimite));
        Assert.Equal(74, Encoding.UTF8.GetByteCount(acimaDoLimite));

        var hash = Senhas.Hash(noLimite);
        Assert.True(Senhas.Confere(noLimite, hash));

        Assert.Throws<BusinessValidationException>(() => Senhas.Hash(acimaDoLimite));
    }

    /// <summary>Emoji ocupa 4 bytes: 19 deles estouram o limite mesmo sendo "so 19 caracteres".</summary>
    [Fact]
    public void Hash_SenhaComEmojiAcimaDeSetentaEDoisBytes_LancaBusinessValidationException()
    {
        var senha = string.Concat(Enumerable.Repeat("\U0001F600", 19)); // 19 * 4 = 76 bytes

        Assert.Equal(76, Encoding.UTF8.GetByteCount(senha));
        Assert.True(Encoding.UTF8.GetByteCount(senha) > Senhas.MaximoBytes);
        Assert.Throws<BusinessValidationException>(() => Senhas.Hash(senha));
    }

    // ---------------------------------------------------------------- Confere

    [Fact]
    public void Confere_SenhaCorretaParaOHash_RetornaVerdadeiro()
    {
        Assert.True(Senhas.Confere(SenhaConhecida, HashDaSenhaConhecida.Value));
    }

    [Theory]
    [InlineData("Gl0rific!2025")]   // um caractere diferente
    [InlineData("gl0rific!2026")]   // caixa diferente (BCrypt e case-sensitive)
    [InlineData("Gl0rific!2026 ")]  // espaco no fim nao pode ser aparado
    public void Confere_SenhaErrada_RetornaFalso(string senha)
    {
        Assert.False(Senhas.Confere(senha, HashDaSenhaConhecida.Value));
    }

    /// <summary>Hash nulo/vazio: conta criada por login do Google nunca definiu senha local.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Confere_HashAusente_RetornaFalsoSemLancar(string? hash)
    {
        Assert.False(Senhas.Confere(SenhaConhecida, hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Confere_SenhaNulaOuVazia_RetornaFalsoSemLancar(string? senha)
    {
        Assert.False(Senhas.Confere(senha, HashDaSenhaConhecida.Value));
    }

    /// <summary>
    /// Hash corrompido no banco nao pode virar 500. Para o cliente e apenas credencial invalida.
    /// </summary>
    [Theory]
    [InlineData("hash-corrompido-no-banco")]
    [InlineData("nao-e-bcrypt")]
    [InlineData("x")]
    [InlineData("$1$12$abcdefghijklmnopqrstuv")]
    public void Confere_HashCorrompido_RetornaFalsoEmVezDeLancar(string hash)
    {
        Assert.False(Senhas.Confere(SenhaConhecida, hash));
    }

    /// <summary>Senha acima de 72 bytes nao explode no login: Confere nao valida tamanho, so falha.</summary>
    [Fact]
    public void Confere_SenhaAcimaDoLimiteDeBytes_RetornaFalsoSemLancar()
    {
        var senha = new string('a', 200);

        Assert.False(Senhas.Confere(senha, HashDaSenhaConhecida.Value));
    }

    // ---------------------------------------------------------------- Equalizar

    /// <summary>
    /// Equalizar existe para o login gastar o mesmo tempo quando o e-mail nao existe. Ela e
    /// chamada no caminho de erro, entao NUNCA pode lancar — nem com senha nula, vazia ou
    /// absurdamente longa. Se lancasse, o oraculo de e-mails voltaria como um 500 seletivo.
    /// </summary>
    [Fact]
    public void Equalizar_QualquerEntrada_NaoLanca()
    {
        var entradas = new string?[]
        {
            null,
            string.Empty,
            "   ",
            "senha-normal",
            new string('a', 200),
            string.Concat(Enumerable.Repeat("\U0001F600", 19))
        };

        foreach (var entrada in entradas)
        {
            var excecao = Record.Exception(() => Senhas.Equalizar(entrada));
            Assert.Null(excecao);
        }
    }

    /// <summary>
    /// O hash descartavel e Lazy: calculado uma vez por processo. Chamar de novo nao pode
    /// lancar nem recalcular — se recalculasse, o caminho de e-mail inexistente ficaria
    /// MAIS lento que o de senha errada e o oraculo voltaria pelo outro lado.
    /// </summary>
    [Fact]
    public void Equalizar_ChamadaRepetida_ContinuaSemLancar()
    {
        Senhas.Equalizar("senha-inexistente");
        Senhas.Equalizar("senha-inexistente");
    }
}
