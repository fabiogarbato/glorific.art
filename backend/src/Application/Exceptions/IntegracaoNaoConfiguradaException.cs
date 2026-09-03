namespace Glorific.Application.Exceptions;

/// <summary>
/// Uma integracao externa foi CHAMADA sem estar configurada (chave ausente ou ainda com o
/// placeholder do appsettings versionado).
///
/// Existe porque este erro nao cabe em nenhuma das duas caixas que ja havia:
///
/// - <see cref="BusinessValidationException"/> (400) diria ao cliente "o dado que voce mandou
///   esta errado". Nao esta: o id_token dele pode ser perfeitamente valido.
/// - Excecao generica cai no 500 "Ocorreu um erro inesperado. Informe o traceId ao suporte.",
///   que e a pior resposta possivel aqui — ela manda o front, o lojista e o suporte procurarem
///   um bug que nao existe, quando a causa e uma variavel de ambiente em branco.
///
/// Por isso ela carrega DUAS mensagens: a tecnica (<see cref="Exception.Message"/>, com o nome
/// exato da chave de configuracao, que vai para o log) e a <see cref="MensagemPublica"/>, que e
/// a unica que chega ao navegador. O nome da variavel de ambiente NAO vaza para o cliente.
/// </summary>
public sealed class IntegracaoNaoConfiguradaException : InvalidOperationException
{
    public IntegracaoNaoConfiguradaException(string integracao, string mensagemPublica, string mensagemTecnica)
        : base(mensagemTecnica)
    {
        Integracao = integracao;
        MensagemPublica = mensagemPublica;
    }

    /// <summary>Rotulo curto da integracao ("Google", "InfinitePay"). So para o log.</summary>
    public string Integracao { get; }

    /// <summary>A unica mensagem que o cliente pode ver. Sem nome de variavel, sem caminho.</summary>
    public string MensagemPublica { get; }
}
