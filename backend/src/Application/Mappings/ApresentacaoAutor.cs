namespace Glorific.Application.Mappings;

/// <summary>
/// Como o nome de quem avaliou aparece na vitrine.
///
/// Publica de proposito: o mapeamento do Mapster e compilado em tempo de execucao a partir de uma
/// arvore de expressao, e chamar metodo privado de outro tipo de dentro dela quebra por
/// visibilidade. Ficar publico e o preco de manter a regra em um lugar so.
///
/// A regra em si e de privacidade: pagina publica de produto nao exibe nome completo de cliente,
/// e nunca exibe e-mail.
/// </summary>
public static class ApresentacaoAutor
{
    private const string Anonimo = "Cliente";

    /// <summary>"Maria Aparecida Silva" vira "Maria A.". Nome vazio vira "Cliente".</summary>
    public static string Abreviar(string? nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
            return Anonimo;

        var partes = nomeCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length == 0)
            return Anonimo;

        if (partes.Length == 1)
            return partes[0];

        return $"{partes[0]} {char.ToUpperInvariant(partes[^1][0])}.";
    }
}
