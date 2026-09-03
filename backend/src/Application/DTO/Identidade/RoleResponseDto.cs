namespace Glorific.Application.DTO.Identidade;

/// <summary>
/// Papel do sistema como sai na API (listagem de papeis do painel admin).
///
/// Serve tambem de modelo do padrao de DTO do projeto: sealed record, { get; init; },
/// herdando o marcador da familia (ResponseDto), sem nenhuma navegacao de entidade dentro.
/// </summary>
public sealed record RoleResponseDto : ResponseDto
{
    public int Id { get; init; }

    /// <summary>Minusculo e sem espaco: e o valor da claim role no JWT.</summary>
    public string Nome { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    /// <summary>Papel que da acesso ao painel administrativo (admin, gerente ou operador).</summary>
    public bool Administrativo { get; init; }
}
