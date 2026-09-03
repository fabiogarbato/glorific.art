namespace Glorific.Domain.Constants;

/// <summary>Papeis do sistema. Sao linhas na tabela roles — nunca string livre em coluna de usuario.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Gerente = "gerente";
    public const string Operador = "operador";
    public const string Cliente = "cliente";

    public static readonly IReadOnlyList<string> Todos = [Admin, Gerente, Operador, Cliente];

    /// <summary>Papeis que dao acesso ao painel administrativo.</summary>
    public static readonly IReadOnlyList<string> Administrativos = [Admin, Gerente, Operador];
}
