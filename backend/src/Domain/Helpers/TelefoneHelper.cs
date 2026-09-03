using System.Text.RegularExpressions;

namespace Glorific.Domain.Helpers;

public static class TelefoneHelper
{
    public static string SomenteDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : Regex.Replace(valor, @"\D", string.Empty);

    /// <summary>Aceita fixo (10) e celular (11) com DDD. Rejeita DDD invalido.</summary>
    public static bool Valido(string? telefone)
    {
        var d = SomenteDigitos(telefone);
        if (d.Length is not (10 or 11)) return false;

        var ddd = int.Parse(d[..2]);
        if (ddd < 11 || ddd > 99) return false;

        // Celular no Brasil comeca com 9 apos o DDD.
        return d.Length != 11 || d[2] == '9';
    }

    public static string Formatar(string? telefone)
    {
        var d = SomenteDigitos(telefone);
        return d.Length switch
        {
            10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
            11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
            _ => d
        };
    }
}
