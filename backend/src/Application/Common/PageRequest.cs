namespace Glorific.Application.Common;

/// <summary>
/// Entrada de paginacao ja NORMALIZADA. O construtor corrige valores impossiveis em vez de
/// lancar: pagina 0 vira 1, tamanho 5000 vira o teto. Sem teto, um "?pageSize=999999" na
/// query string vira negacao de servico de graca.
/// </summary>
public sealed record PageRequest
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public PageRequest() { }

    public PageRequest(int? pagina, int? tamanhoPagina)
    {
        Page = pagina is null or < 1 ? 1 : pagina.Value;
        PageSize = tamanhoPagina switch
        {
            null or < 1 => TamanhoPadrao,
            > TamanhoMaximo => TamanhoMaximo,
            _ => tamanhoPagina.Value
        };
    }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = TamanhoPadrao;

    /// <summary>Quantos registros pular. Usado direto no Skip do repositorio.</summary>
    public int Skip => (Page - 1) * PageSize;

    public int Take => PageSize;
}
