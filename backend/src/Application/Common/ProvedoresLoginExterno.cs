namespace Glorific.Application.Common;

/// <summary>
/// Nomes dos provedores de login externo, exatamente como sao gravados em
/// logins_externos.provedor (minusculo, sem espaco).
///
/// Constante e nao string repetida porque o valor entra em WHERE de indice unico
/// (provedor, subject_id): um "Google" com maiuscula em um unico ponto do codigo cria um
/// SEGUNDO vinculo para o mesmo usuario e o login passa a alternar entre duas contas.
/// </summary>
public static class ProvedoresLoginExterno
{
    public const string Google = "google";
}
