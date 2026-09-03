using System.Text.RegularExpressions;

namespace Glorific.Domain.Helpers;

/// <summary>
/// Validacao e normalizacao de CPF/CNPJ. Funcoes puras estaticas.
/// O CPF do destinatario e obrigatorio no checkout: transportadora exige documento e,
/// sem ele, a etiqueta falha DEPOIS do cliente ja ter pago.
/// </summary>
public static class DocumentoHelper
{
    public static string SomenteDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : Regex.Replace(valor, @"\D", string.Empty);

    public static bool CpfValido(string? cpf)
    {
        var d = SomenteDigitos(cpf);
        if (d.Length != 11) return false;
        // Rejeita sequencias iguais (00000000000, 11111111111...), que passam no calculo do DV.
        if (d.All(c => c == d[0])) return false;

        var primeiro = CalcularDigito(d, 9, 10);
        var segundo = CalcularDigito(d, 10, 11);
        return d[9] == primeiro && d[10] == segundo;
    }

    public static bool CnpjValido(string? cnpj)
    {
        var d = SomenteDigitos(cnpj);
        if (d.Length != 14) return false;
        if (d.All(c => c == d[0])) return false;

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var primeiro = CalcularDigitoCnpj(d, pesos1);
        var segundo = CalcularDigitoCnpj(d, pesos2);
        return d[12] == primeiro && d[13] == segundo;
    }

    /// <summary>Formata para exibicao. Devolve o valor original se nao for CPF/CNPJ valido em tamanho.</summary>
    public static string Formatar(string? documento)
    {
        var d = SomenteDigitos(documento);
        return d.Length switch
        {
            11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
            14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
            _ => d
        };
    }

    private static char CalcularDigito(string digitos, int quantidade, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
            soma += (digitos[i] - '0') * (pesoInicial - i);

        var resto = soma % 11;
        return resto < 2 ? '0' : (char)('0' + (11 - resto));
    }

    private static char CalcularDigitoCnpj(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (digitos[i] - '0') * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? '0' : (char)('0' + (11 - resto));
    }
}
