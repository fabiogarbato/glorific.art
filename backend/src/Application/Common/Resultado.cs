using Glorific.Application.Exceptions;

namespace Glorific.Application.Common;

/// <summary>
/// Resultado de uma operacao de negocio que PODE falhar sem que isso seja excepcional.
///
/// Por que existir junto com BusinessValidationException: excecao e cara e, pior, e binaria.
/// No loop de reserva de estoque do checkout precisamos saber QUAIS itens falharam para montar
/// a mensagem "Tamanho M em Terracota esgotado" com todos os itens de uma vez — lancar no
/// primeiro item perde os demais e paga o custo de stack unwinding N vezes.
///
/// Regra de uso: Resultado para o caminho previsivel dentro de um laco ou de uma validacao em
/// lote; BusinessValidationException na fronteira do caso de uso, quando o erro tem de virar
/// 400 e abortar tudo. LancarSeFalhou() e a ponte entre os dois.
/// </summary>
public sealed record Resultado
{
    private Resultado(bool sucesso, string? erro, string? codigo)
    {
        Sucesso = sucesso;
        Erro = erro;
        Codigo = codigo;
    }

    public bool Sucesso { get; }

    public bool Falhou => !Sucesso;

    /// <summary>Mensagem pronta para o usuario final. Null quando Sucesso.</summary>
    public string? Erro { get; }

    /// <summary>Codigo estavel opcional (ex.: "estoque_insuficiente") para o front decidir a UX.</summary>
    public string? Codigo { get; }

    public static Resultado Ok() => new(true, null, null);

    public static Resultado Falha(string mensagem, string? codigo = null) =>
        new(false, string.IsNullOrWhiteSpace(mensagem) ? "Operacao invalida." : mensagem, codigo);

    /// <summary>Converte a falha na excecao de negocio da camada. No-op quando Sucesso.</summary>
    public void LancarSeFalhou()
    {
        if (Falhou) throw new BusinessValidationException(Erro!);
    }

    public static Resultado De(bool condicao, string mensagemDeFalha, string? codigo = null) =>
        condicao ? Ok() : Falha(mensagemDeFalha, codigo);

    /// <summary>
    /// Agrega varios resultados num so. Todas as mensagens de falha sao preservadas e unidas —
    /// e exatamente o caso do laco de itens do carrinho.
    /// </summary>
    public static Resultado Combinar(IEnumerable<Resultado> resultados)
    {
        var falhas = resultados.Where(r => r.Falhou).Select(r => r.Erro!).ToArray();
        return falhas.Length == 0 ? Ok() : Falha(string.Join(" ", falhas));
    }
}

/// <summary>
/// Resultado que carrega um valor no caminho feliz. Nao herda de <see cref="Resultado"/> de
/// proposito: heranca de record com igualdade estrutural entre tipos abertos e fechados e uma
/// armadilha silenciosa. Use <see cref="SemValor"/> quando precisar do resultado sem o payload.
/// </summary>
public sealed record Resultado<T>
{
    private Resultado(bool sucesso, T? valor, string? erro, string? codigo)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erro = erro;
        Codigo = codigo;
    }

    public bool Sucesso { get; }

    public bool Falhou => !Sucesso;

    /// <summary>Preenchido apenas quando Sucesso. Nao acesse sem checar.</summary>
    public T? Valor { get; }

    public string? Erro { get; }

    public string? Codigo { get; }

    public static Resultado<T> Ok(T valor) => new(true, valor, null, null);

    public static Resultado<T> Falha(string mensagem, string? codigo = null) =>
        new(false, default, string.IsNullOrWhiteSpace(mensagem) ? "Operacao invalida." : mensagem, codigo);

    public Resultado SemValor() => Sucesso ? Resultado.Ok() : Resultado.Falha(Erro!, Codigo);

    /// <summary>Devolve o valor ou lanca a excecao de negocio com a mensagem da falha.</summary>
    public T ValorOuLancar()
    {
        if (Falhou) throw new BusinessValidationException(Erro!);
        return Valor!;
    }
}
