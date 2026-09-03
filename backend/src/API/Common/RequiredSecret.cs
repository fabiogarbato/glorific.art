using Glorific.Application.Ports.Options;

namespace Glorific.Api.Common;

/// <summary>
/// Fail-fast de segredo no boot, em TODOS os ambientes.
///
/// O modo de falha que isto evita: a API sobe "com sucesso", o Jwt:Key fica com o placeholder do
/// appsettings versionado, e todo mundo que loga recebe um token assinado com uma chave que esta
/// no Git. Ninguem percebe porque nada quebra — ate alguem perceber.
///
/// A mensagem diz o nome da chave de configuracao E o nome da variavel de ambiente, porque a
/// traducao ":" -> "__" e exatamente onde o deploy erra.
///
/// DETALHE QUE JA CUSTOU CARO: o valor volta com Trim() aplicado, e quem chama DEVE usar o valor
/// retornado. No repo de referencia o boot validava com Trim e o servico relia a configuracao
/// crua; uma env var com quebra de linha no fim passava na validacao e invalidava a assinatura
/// de todos os tokens em runtime.
/// </summary>
public static class RequiredSecret
{
    /// <summary>
    /// Valor do appsettings versionado. Fonte da verdade de "isto NAO foi configurado":
    /// tratar so vazio como ausente deixa o placeholder passar batido.
    ///
    /// A constante mora na Application (<see cref="SegredoPlaceholder"/>) porque os adaptadores
    /// da Infrastructure precisam do MESMO valor para reconferir em runtime. Duas copias da
    /// string seriam duas definicoes de "nao configurado" que um dia divergem.
    /// </summary>
    public const string Placeholder = SegredoPlaceholder.Valor;

    /// <summary>
    /// Exige a chave e devolve o valor ja normalizado com Trim.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <param name="chaveConfiguracao">Ex.: "Jwt:Key".</param>
    /// <param name="variavelAmbiente">Ex.: "Jwt__Key". So aparece na mensagem de erro.</param>
    /// <param name="tamanhoMinimo">Comprimento minimo aceitavel apos o Trim. 0 desliga.</param>
    public static string Require(
        IConfiguration configuration,
        string chaveConfiguracao,
        string variavelAmbiente,
        int tamanhoMinimo = 0)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveConfiguracao);

        var valor = configuration[chaveConfiguracao]?.Trim();

        if (string.IsNullOrWhiteSpace(valor))
            throw Falhar(chaveConfiguracao, variavelAmbiente, "nao esta definida");

        if (string.Equals(valor, Placeholder, StringComparison.Ordinal))
            throw Falhar(
                chaveConfiguracao,
                variavelAmbiente,
                $"ainda esta com o placeholder '{Placeholder}' do appsettings versionado");

        if (tamanhoMinimo > 0 && valor.Length < tamanhoMinimo)
            throw Falhar(
                chaveConfiguracao,
                variavelAmbiente,
                $"tem {valor.Length} caracteres e o minimo exigido e {tamanhoMinimo}");

        return valor;
    }

    /// <summary>
    /// Mesma exigencia, mas so quando a condicao for verdadeira.
    ///
    /// Uso previsto: segredos de integracao externa (Google, gateway de pagamento, Melhor
    /// Envio) que sao obrigatorios em producao e homologacao, mas nao devem impedir um
    /// desenvolvedor de subir a API local para mexer no catalogo.
    /// </summary>
    public static string? RequireSe(
        bool condicao,
        IConfiguration configuration,
        string chaveConfiguracao,
        string variavelAmbiente,
        int tamanhoMinimo = 0)
    {
        if (!condicao)
            return configuration[chaveConfiguracao]?.Trim();

        return Require(configuration, chaveConfiguracao, variavelAmbiente, tamanhoMinimo);
    }

    private static InvalidOperationException Falhar(
        string chaveConfiguracao,
        string variavelAmbiente,
        string motivo) =>
        new(
            $"BOOT ABORTADO. A configuracao obrigatoria '{chaveConfiguracao}' {motivo}. " +
            $"Defina a variavel de ambiente {variavelAmbiente} (ou a secao correspondente do " +
            "appsettings do ambiente) antes de iniciar a API.");
}
