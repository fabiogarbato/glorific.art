using Glorific.Domain.Common;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Carrinho;

/// <summary>
/// Carrinho server-side. O repo de referencia so tinha carrinho em localStorage, o que custa
/// tres coisas: recuperacao de carrinho abandonado (a alavanca de receita numero 1 em moda),
/// sobrevivencia a troca de dispositivo depois do login Google, e a possibilidade de avisar
/// "o preco deste item mudou" em vez de cobrar surpresa no checkout.
///
/// Carrinho NAO reserva estoque: reservar no "adicionar ao carrinho" trava peca de giro rapido
/// para quem nao vai comprar. A autoridade sobre disponibilidade e o POST /checkout.
///
/// IdUsuario e ChaveSessao convivem porque o carrinho nasce anonimo (cookie) e e adotado no login.
/// </summary>
public class Carrinho : BaseEntity, IAuditable
{
    /// <summary>Identificador publico — o Id inteiro nunca sai para o front.</summary>
    public required string Uuid { get; set; }

    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Cookie do visitante anonimo. Vira null quando o carrinho e adotado no login.</summary>
    public string? ChaveSessao { get; set; }

    public StatusCarrinho Status { get; set; } = StatusCarrinho.Aberto;

    public int? IdCupom { get; set; }
    public Cupom? Cupom { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }

    /// <summary>Prazo do worker de abandono. Passou disso, o carrinho e marcado Expirado.</summary>
    public DateTime ExpiraEm { get; set; }

    public ICollection<CarrinhoItem> Itens { get; set; } = [];
}
