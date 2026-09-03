using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Config;

/// <summary>
/// Segredo de integracao guardado cifrado (AES-256-GCM com a chave mestra vinda de env).
/// Fica no banco, e nao so em variavel de ambiente, porque token do Melhor Envio expira e
/// precisa ser rotacionado pelo admin sem redeploy do container.
///
/// A propriedade e Chave, mas a coluna e config_key: key e palavra reservada no Postgres.
/// EhSegredo separa o que pode ser exibido em claro no painel do que so pode ser sobrescrito.
/// </summary>
public class AppSecret : BaseEntity, IAuditable
{
    public required string Chave { get; set; }

    /// <summary>Texto cifrado. O valor em claro nunca e persistido nem logado.</summary>
    public required string ValorCriptografado { get; set; }

    public bool EhSegredo { get; set; } = true;

    public string? Descricao { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }
}
