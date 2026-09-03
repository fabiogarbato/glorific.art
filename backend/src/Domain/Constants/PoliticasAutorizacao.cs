namespace Glorific.Domain.Constants;

/// <summary>Nomes das policies. Constante para o controller nao depender de string magica.</summary>
public static class PoliticasAutorizacao
{
    /// <summary>Somente admin: usuarios, segredos, configuracao da loja.</summary>
    public const string SomenteAdmin = "SomenteAdmin";

    /// <summary>Admin ou gerente: catalogo, preco, cupom, moderacao.</summary>
    public const string GestaoCatalogo = "GestaoCatalogo";

    /// <summary>Admin, gerente ou operador: pedidos, expedicao, etiquetas.</summary>
    public const string Expedicao = "Expedicao";

    /// <summary>Qualquer papel administrativo — porta de entrada do painel.</summary>
    public const string PainelAdmin = "PainelAdmin";
}
