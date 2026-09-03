using Glorific.Application.Exceptions;
using Glorific.Domain.Helpers;

namespace Glorific.Application.Common;

/// <summary>
/// Gera slug UNICO a partir de um texto, desambiguando com sufixo numerico.
///
/// Por que nao deixar o banco resolver: o indice unico de slug existe em categorias, colecoes,
/// cores e produtos, e duas pecas com o mesmo nome ("Vestido Linho") sao comuns em moda.
/// Sem a desambiguacao aqui, o cadastro passa na validacao e estoura com violacao de indice —
/// erro 500 cru na tela do admin em vez de um slug "vestido-linho-2" gerado sozinho.
///
/// O predicado de existencia vem de fora porque cada agregado tem o seu (SlugEmUsoAsync do
/// repositorio correspondente), sempre com IgnoreQueryFilters onde ha soft delete: registro
/// desativado continua ocupando o slug no indice.
/// </summary>
public static class GeradorSlug
{
    /// <summary>
    /// Teto de tentativas. Existe para o laco nao virar consulta infinita ao banco caso o
    /// predicado tenha um bug e responda sempre "em uso".
    /// </summary>
    private const int MaximoTentativas = 200;

    /// <summary>
    /// Devolve o primeiro slug livre derivado de <paramref name="textoBase"/>.
    /// O primeiro candidato e o slug puro; a partir do segundo entra o sufixo (-2, -3, ...).
    /// </summary>
    public static async Task<string> UnicoAsync(
        string? textoBase,
        Func<string, CancellationToken, Task<bool>> slugEmUso,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slugEmUso);

        var raiz = SlugHelper.Gerar(textoBase);

        if (string.IsNullOrWhiteSpace(raiz))
            throw new BusinessValidationException(
                "Nao foi possivel gerar o endereco (slug): informe um nome com letras ou numeros.");

        for (var sufixo = 1; sufixo <= MaximoTentativas; sufixo++)
        {
            var candidato = SlugHelper.ComSufixo(raiz, sufixo);

            if (!await slugEmUso(candidato, cancellationToken))
                return candidato;
        }

        throw new BusinessValidationException(
            $"Nao foi possivel gerar um endereco (slug) unico a partir de '{raiz}'. Altere o nome.");
    }

    /// <summary>
    /// Normaliza o slug informado manualmente pelo admin e garante unicidade. Quando o campo
    /// vem vazio, cai na derivacao pelo <paramref name="textoBase"/>.
    /// </summary>
    public static Task<string> UnicoAsync(
        string? slugInformado,
        string? textoBase,
        Func<string, CancellationToken, Task<bool>> slugEmUso,
        CancellationToken cancellationToken = default) =>
        UnicoAsync(
            string.IsNullOrWhiteSpace(slugInformado) ? textoBase : slugInformado,
            slugEmUso,
            cancellationToken);
}
