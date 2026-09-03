namespace Glorific.Application.Exceptions;

/// <summary>
/// UNICA excecao de negocio da camada Application. O middleware da API traduz para 400 com o
/// envelope { statusCode, error, traceId, errors? }.
///
/// Regra dura herdada do repo de referencia: servico nao lanca Exception generica nem
/// InvalidOperationException para regra de negocio — aquilo caia em 500 e escondia erro de
/// usuario dentro de alerta de infraestrutura. Se e culpa do input, e esta excecao.
///
/// Erros carrega o detalhamento por campo quando a validacao e composta (ex.: varios itens do
/// carrinho esgotados de uma vez), sem obrigar o chamador a fazer parse da mensagem.
/// </summary>
public class BusinessValidationException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> SemDetalhe =
        new Dictionary<string, string[]>();

    public BusinessValidationException(string mensagem)
        : base(mensagem)
    {
        Erros = SemDetalhe;
    }

    public BusinessValidationException(string mensagem, Exception innerException)
        : base(mensagem, innerException)
    {
        Erros = SemDetalhe;
    }

    public BusinessValidationException(string mensagem, IReadOnlyDictionary<string, string[]> erros)
        : base(mensagem)
    {
        Erros = erros ?? SemDetalhe;
    }

    /// <summary>Campo -> mensagens. Vazio quando o erro nao e por campo.</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public bool TemDetalhe => Erros.Count > 0;

    /// <summary>Guarda de pre-condicao. Evita o if/throw repetido no topo de cada caso de uso.</summary>
    public static void LancarSe(bool condicao, string mensagem)
    {
        if (condicao) throw new BusinessValidationException(mensagem);
    }

    public static void LancarSeVazio(string? valor, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new BusinessValidationException(mensagem);
    }
}
