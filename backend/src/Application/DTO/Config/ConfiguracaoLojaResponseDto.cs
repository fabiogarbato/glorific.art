namespace Glorific.Application.DTO.Config;

/// <summary>
/// Configuracao operacional da loja.
///
/// E este DTO, e nao a entidade, que fica no cache de memoria: record imutavel nao pode ser
/// alterado por engano por um consumidor e nao carrega ChangeTracker junto. Guardar a entidade
/// rastreada num cache de processo vazaria o DbContext de uma requisicao para todas as outras.
/// </summary>
public sealed record ConfiguracaoLojaResponseDto : ResponseDto
{
    public int Id { get; init; }

    /// <summary>Acima deste valor o frete sai zerado. Null desliga a regra.</summary>
    public int? FreteGratisAcimaDeCentavos { get; init; }

    public int PrazoManuseioDias { get; init; }

    /// <summary>CEP de origem das cotacoes, so digitos.</summary>
    public string CepOrigem { get; init; } = string.Empty;

    public int PoliticaTrocaDias { get; init; }

    public int? PedidoMinimoCentavos { get; init; }

    public bool ExibirEstoqueBaixo { get; init; }

    public int LimiteEstoqueBaixo { get; init; }

    public DateTime DataCriacao { get; init; }

    public DateTime? DataAlteracao { get; init; }
}
