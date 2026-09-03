using System.Text.RegularExpressions;

namespace Glorific.Domain.Helpers;

public static class CepHelper
{
    public static string SomenteDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : Regex.Replace(valor, @"\D", string.Empty);

    public static bool Valido(string? cep)
    {
        var d = SomenteDigitos(cep);
        return d.Length == 8 && !d.All(c => c == '0');
    }

    public static string Formatar(string? cep)
    {
        var d = SomenteDigitos(cep);
        return d.Length == 8 ? $"{d[..5]}-{d[5..]}" : d;
    }
}
