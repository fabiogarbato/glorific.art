using Glorific.Domain.Enums;

namespace Glorific.Application.DTO.Painel;

/// <summary>
/// Linha da fila de envio travada.
///
/// Aparece aqui o envio em Falha e tambem o que ja tentou pelo menos uma vez e continua pendente:
/// esperar o status virar Falha para avisar o operador significa descobrir o problema depois do
/// backoff inteiro ter rodado, com o cliente ja cobrando o codigo de rastreio.
/// </summary>
public sealed record DashboardEnvioProblemaDto : ResponseDto
{
    public int IdEnvio { get; init; }

    public int IdPedido { get; init; }

    public string NumeroPedido { get; init; } = string.Empty;

    public StatusEnvio Status { get; init; }

    public string StatusNome { get; init; } = string.Empty;

    public int Tentativas { get; init; }

    public string? UltimoErro { get; init; }

    public DateTime? ProximaTentativaEm { get; init; }
}
