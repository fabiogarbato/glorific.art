using Glorific.Domain.Common;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;

namespace Glorific.Domain.Entities.Social;

/// <summary>
/// Avaliacao de produto com os campos que so fazem sentido em moda.
///
/// IdPedidoItem e o que sustenta o selo "compra verificada" e bloqueia review de quem nao comprou.
/// CaimentoTamanho com AlturaCliente e PesoCliente e o motivo principal de existir avaliacao em
/// loja de roupa: "veste pequeno, peguei um numero acima" e o que reduz devolucao.
///
/// Status nasce Pendente por decisao de risco reputacional — loja crista com comentario aberto
/// sem moderacao vira problema de marca, nao de produto.
/// </summary>
public class Avaliacao : BaseEntity
{
    public int IdProduto { get; set; }
    public Produto Produto { get; set; } = null!;

    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Null quando a avaliacao nao pode ser amarrada a uma compra.</summary>
    public int? IdPedidoItem { get; set; }
    public PedidoItem? PedidoItem { get; set; }

    /// <summary>De 1 a 5, com CHECK no banco.</summary>
    public int Nota { get; set; }

    public string? Titulo { get; set; }
    public string? Comentario { get; set; }

    /// <summary>Codigo do tamanho como texto: o cliente comprou "M" mesmo que a grade mude depois.</summary>
    public string? TamanhoComprado { get; set; }

    public int? AlturaClienteCm { get; set; }
    public decimal? PesoClienteKg { get; set; }

    public CaimentoTamanho? Caimento { get; set; }

    public bool? Recomenda { get; set; }

    public StatusAvaliacao Status { get; set; } = StatusAvaliacao.Pendente;

    public string? MotivoRejeicao { get; set; }

    public int? ModeradaPor { get; set; }
    public Usuario? UsuarioModerador { get; set; }
    public DateTime? ModeradaEm { get; set; }

    public DateTime DataCriacao { get; set; }

    public ICollection<AvaliacaoMidia> Midias { get; set; } = [];
}
