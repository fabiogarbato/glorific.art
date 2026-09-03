using Glorific.Domain.Common;
using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Entities.Social;

// A pasta Carrinho e ao mesmo tempo namespace e nome de entidade; sem o alias o compilador
// resolve "Carrinho" como namespace ao subir para Glorific.Domain.Entities.
using CarrinhoDoUsuario = Glorific.Domain.Entities.Carrinho.Carrinho;

namespace Glorific.Domain.Entities.Identidade;

/// <summary>
/// Cliente e operador sao o MESMO usuario; o que separa e o papel (usuarios_roles).
/// O repo de referencia guardava o papel numa coluna varchar com normalizacao em setter,
/// o que aceitava "superuser" sem erro e estourava NRE no backing field nao inicializado.
///
/// SenhaHash e nullable de proposito: quem entrou por Google nunca definiu senha, e obrigar
/// uma senha fake para satisfazer o NOT NULL cria credencial adivinhavel.
/// </summary>
public class Usuario : BaseEntity, IAuditable
{
    /// <summary>Identificador publico. Um unico formato em todo o sistema: Guid com hifens.</summary>
    public required string Uuid { get; set; }

    /// <summary>Normalizado em minusculas antes de gravar — senao o mesmo e-mail entra duas vezes.</summary>
    public required string Email { get; set; }

    public bool EmailVerificado { get; set; }

    public string? NomeCompleto { get; set; }

    /// <summary>So digitos. Unico apenas quando preenchido (indice parcial no banco).</summary>
    public string? Cpf { get; set; }

    public string? Telefone { get; set; }

    /// <summary>Null para usuario que existe apenas via provedor externo.</summary>
    public string? SenhaHash { get; set; }

    public string? FotoUrl { get; set; }
    public DateTime? DataNascimento { get; set; }

    public bool AceitaMarketing { get; set; }

    /// <summary>Soft delete: pedidos e avaliacoes antigos continuam apontando para este usuario.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime? UltimoLoginEm { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public ICollection<UsuarioRole> Roles { get; set; } = [];
    public ICollection<LoginExterno> LoginsExternos { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Endereco> Enderecos { get; set; } = [];
    public ICollection<ListaDesejoItem> ListaDesejo { get; set; } = [];
    public ICollection<Pedido> Pedidos { get; set; } = [];
    public ICollection<Avaliacao> Avaliacoes { get; set; } = [];
    public ICollection<CarrinhoDoUsuario> Carrinhos { get; set; } = [];
}
