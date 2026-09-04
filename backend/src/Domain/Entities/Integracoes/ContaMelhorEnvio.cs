using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Integracoes;

/// <summary>
/// Linha unica com o token OAuth da conta do Melhor Envio (mesmo padrao de
/// ConfiguracaoLoja: sem lista, sem delete, so obter/alterar).
///
/// O ACCESS TOKEN expira rapido (o ME usa ~30 dias, mas o campo aqui e generico); quem PRECISA
/// de um token valido chama o adaptador (MelhorEnvioClient), que le esta linha, confere
/// ExpiraEmUtc e renova sozinho via RefreshToken antes de qualquer chamada de negocio — nenhum
/// servico de catalogo/pedido decide isso.
/// </summary>
public class ContaMelhorEnvio : BaseEntity
{
    /// <summary>MelhorEnvio:ContaId nas options (hoje sempre "glorific" — single tenant).</summary>
    public required string ContaId { get; set; }

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    /// <summary>"Bearer", como o ME devolve.</summary>
    public string? TipoToken { get; set; }

    /// <summary>Escopos concedidos, separados por espaco.</summary>
    public string? Escopo { get; set; }

    public DateTime? ExpiraEmUtc { get; set; }

    public DateTime? AtualizadoEmUtc { get; set; }
}
