using System.Text.RegularExpressions;

namespace Glorific.Application.Exceptions;

/// <summary>
/// Falha vinda do microservico integracaoMelhorEnvio.
///
/// POR QUE ELA MORA NO APPLICATION E NAO NA INFRASTRUCTURE: quem lanca e o adaptador HTTP
/// (Infrastructure), mas quem PRECISA CAPTURAR e o servico de negocio (FreteService, o
/// EnvioProcessor). Application nao referencia Infrastructure — se o tipo morasse la, o
/// unico jeito de tratar seria capturar Exception generica e olhar o nome do tipo por string.
/// Infrastructure referencia Application, entao declarar aqui e o unico lugar em que os dois
/// lados enxergam o mesmo tipo sem inverter a dependencia.
///
/// Os servicos tratam por ESTAS PROPRIEDADES, nunca por status code cru: o status aqui e o
/// HTTP do Melhor Envio repassado pelo microservico, e um 404 significa "conta nao conectada"
/// (problema operacional nosso), nao "recurso inexistente" (erro do cliente).
/// </summary>
public class MelhorEnvioApiException : Exception
{
    /// <summary>Trecho que o microservico anexa ao detail quando repassa erro cru do ME.</summary>
    private const string MarcadorCorpo = ". Corpo:";

    public MelhorEnvioApiException(
        string mensagem,
        int? statusCode = null,
        string? corpoBruto = null,
        Exception? innerException = null)
        : base(mensagem, innerException)
    {
        StatusCode = statusCode;
        CorpoBruto = corpoBruto;
    }

    /// <summary>
    /// Status HTTP devolvido pelo microservico. Null quando nem houve resposta: timeout,
    /// conexao recusada, DNS. E a distincao que separa "o parceiro recusou" de "o parceiro
    /// nao respondeu" — a primeira e culpa do dado, a segunda e indisponibilidade.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>Corpo cru da resposta, para log e para gravar em raw_ultima_resposta.</summary>
    public string? CorpoBruto { get; }

    /// <summary>4xx: o dado enviado nao serve. Vira 400 nosso.</summary>
    public bool EhErroCliente => StatusCode is >= 400 and < 500;

    /// <summary>5xx ou ausencia de resposta: o parceiro esta fora. Vira 502 nosso.</summary>
    public bool EhFalhaComunicacao => StatusCode is null or >= 500;

    /// <summary>
    /// 404 "Conta nao conectada" do microservico: a conta do Melhor Envio perdeu a autorizacao
    /// OAuth. NAO e erro do cliente final — e alerta operacional, e a loja para de despachar
    /// ate alguem reautorizar. O repo de referencia tinha este predicado e nenhum consumidor.
    /// </summary>
    public bool EhContaNaoConectada =>
        StatusCode == 404
        && (Contem(Message, "conta nao conectada")
            || Contem(Message, "conta não conectada")
            || Contem(CorpoBruto, "Conta nao conectada")
            || Contem(CorpoBruto, "Conta não conectada"));

    /// <summary>
    /// Mensagem limpa para exibir ao usuario final.
    ///
    /// O microservico entrega o erro de validacao do ME embutido em texto
    /// ("... (HTTP 422). Corpo: {"errors":{"to.postal_code":[...]}}"). Mostrar isso na tela do
    /// cliente vaza JSON de terceiro; aqui o JSON cru e o sufixo "(Parameter 'x')" do
    /// ArgumentException saem fora.
    /// </summary>
    public string DetalheAmigavel => Limpar(Message);

    internal static string Limpar(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            return "Erro nao detalhado pelo servico de frete.";

        var texto = mensagem;

        var corte = texto.IndexOf(MarcadorCorpo, StringComparison.OrdinalIgnoreCase);
        if (corte > 0)
            texto = texto[..corte];

        // "(Parameter 'from.postalCode')" e ruido de ArgumentException do microservico.
        texto = Regex.Replace(texto, @"\s*\(Parameter '[^']*'\)", string.Empty);

        return texto.Trim().TrimEnd('.', ' ') is { Length: > 0 } limpo
            ? limpo
            : "Erro nao detalhado pelo servico de frete.";
    }

    private static bool Contem(string? origem, string trecho) =>
        !string.IsNullOrEmpty(origem)
        && origem.Contains(trecho, StringComparison.OrdinalIgnoreCase);
}
