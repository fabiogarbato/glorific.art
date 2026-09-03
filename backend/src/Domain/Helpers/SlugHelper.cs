using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Glorific.Domain.Helpers;

/// <summary>Gera slug de URL. Catalogo de moda e SEO-critico: /vestidos/midi-linho-off-white.</summary>
public static class SlugHelper
{
    public static string Gerar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        // Decompoe acentos e descarta as marcas diacriticas: "Vestido Túnica" -> "vestido tunica".
        var normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var semAcento = sb.ToString().Normalize(NormalizationForm.FormC);
        var limpo = Regex.Replace(semAcento, @"[^a-z0-9\s-]", string.Empty);
        var comHifen = Regex.Replace(limpo, @"[\s-]+", "-");
        return comHifen.Trim('-');
    }

    /// <summary>Acrescenta sufixo numerico quando o slug ja existe: vestido-linho-2.</summary>
    public static string ComSufixo(string slug, int sufixo) => sufixo <= 1 ? slug : $"{slug}-{sufixo}";
}
