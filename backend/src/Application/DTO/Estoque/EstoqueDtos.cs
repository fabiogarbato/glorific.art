using System.ComponentModel.DataAnnotations;

namespace Glorific.Application.DTO.Estoque;

/// <summary>
/// Saldo de um SKU como sai no painel.
///
/// Os tres numeros aparecem juntos de proposito: Fisico e o que existe na prateleira,
/// Reservado e o que ja esta comprometido em checkout aguardando pagamento e Disponivel e o
/// unico que pode ser vendido. Mostrar so um deles e o que faz o operador achar que ha peca
/// quando ela ja foi vendida — ou o contrario, achar que sumiu quando so esta reservada.
/// </summary>
public sealed record EstoqueVariacaoResponseDto : ResponseDto
{
    public int IdVariacao { get; init; }

    public string Sku { get; init; } = string.Empty;

    public int IdProduto { get; init; }

    public string NomeProduto { get; init; } = string.Empty;

    public string? Tamanho { get; init; }

    public string? Cor { get; init; }

    /// <summary>Estoque fisico.</summary>
    public int Quantidade { get; init; }

    public int QuantidadeReservada { get; init; }

    /// <summary>Quantidade menos reservada. E o que a vitrine pode vender.</summary>
    public int Disponivel { get; init; }

    public int QuantidadeMinima { get; init; }

    public string? Localizacao { get; init; }

    public DateTime? DataUltimaMovimentacao { get; init; }

    /// <summary>Disponivel abaixo do minimo configurado. Alimenta o alerta do painel.</summary>
    public bool AbaixoDoMinimo { get; init; }
}

/// <summary>Linha do ledger de estoque. Append-only: nao existe editar nem apagar.</summary>
public sealed record MovimentacaoEstoqueResponseDto : ResponseDto
{
    public int Id { get; init; }

    public int IdVariacao { get; init; }

    public string? Sku { get; init; }

    public string? NomeProduto { get; init; }

    /// <summary>Rotulo do tipo ("Reabastecimento", "Venda por sistema", "Perda/avaria").</summary>
    public string Movimento { get; init; } = string.Empty;

    /// <summary>Sinalizada: positiva entrada, negativa saida.</summary>
    public int Quantidade { get; init; }

    /// <summary>Fisico ANTES do movimento. Gravado na linha para auditar sem replay do log.</summary>
    public int QuantidadeAntes { get; init; }

    public int QuantidadeDepois { get; init; }

    public int? IdPedido { get; init; }

    public string? Observacao { get; init; }

    public DateTime DataMovimentacao { get; init; }
}

/// <summary>Uma linha de entrada de estoque (nota do fornecedor, producao, devolucao aprovada).</summary>
public sealed record EstoqueEntradaItemDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe a variacao (SKU) da entrada.")]
    public int IdVariacao { get; init; }

    [Range(1, 100000, ErrorMessage = "A quantidade de entrada deve ser maior que zero.")]
    public int Quantidade { get; init; }
}

/// <summary>
/// Entrada de estoque em LOTE.
///
/// Lote e nao unitario porque o cadastro real e uma nota do fornecedor com dezenas de SKUs:
/// uma chamada por linha faria dezenas de transacoes independentes, e uma falha no meio
/// deixaria metade da nota lancada sem ninguem saber qual metade.
/// </summary>
public sealed record EstoqueEntradaDto : CreateDto
{
    [Required(ErrorMessage = "Informe ao menos um item na entrada.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um item na entrada.")]
    public IReadOnlyList<EstoqueEntradaItemDto> Itens { get; init; } = [];

    /// <summary>
    /// Origem da entrada. Aceita apenas os movimentos de ENTRADA do catalogo fechado
    /// (Reabastecimento, Cadastro inicial, Devolucao de cliente) — o servico valida.
    /// </summary>
    public string? Movimento { get; init; }

    [StringLength(500, ErrorMessage = "A observacao deve ter no maximo 500 caracteres.")]
    public string? Observacao { get; init; }
}

/// <summary>
/// Ajuste de inventario: o operador informa a contagem FISICA encontrada, nao o delta.
///
/// Pedir o valor final e nao a diferenca e deliberado — quem conta a prateleira sabe quantas
/// pecas viu, e obrigar a calcular a diferenca de cabeca e a origem classica do ajuste com o
/// sinal trocado. O servico deriva o delta e grava o ledger com antes e depois.
/// </summary>
public sealed record EstoqueAjusteDto : CreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Informe a variacao (SKU) do ajuste.")]
    public int IdVariacao { get; init; }

    [Range(0, 1000000, ErrorMessage = "A quantidade contada nao pode ser negativa.")]
    public int QuantidadeContada { get; init; }

    /// <summary>
    /// Movimento a registrar. Vazio usa "Ajuste de inventario"; "Perda/avaria" e "Venda manual"
    /// tambem sao aceitos, e sao o que separa erro de contagem de peca danificada no relatorio.
    /// </summary>
    public string? Movimento { get; init; }

    [Required(ErrorMessage = "Descreva o motivo do ajuste.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "O motivo deve ter entre 3 e 500 caracteres.")]
    public string Observacao { get; init; } = string.Empty;
}

/// <summary>Parametros do SKU no estoque. Nao mexe em saldo — saldo so muda por movimentacao.</summary>
public sealed record EstoqueParametrosUpdateDto : UpdateDto
{
    [Range(0, 100000, ErrorMessage = "A quantidade minima nao pode ser negativa.")]
    public int QuantidadeMinima { get; init; }

    [StringLength(100, ErrorMessage = "A localizacao deve ter no maximo 100 caracteres.")]
    public string? Localizacao { get; init; }
}

/// <summary>Filtro do extrato de movimentacoes do painel.</summary>
public sealed record MovimentacaoEstoqueFiltro
{
    public int? IdVariacao { get; init; }

    public int? IdPedido { get; init; }

    /// <summary>Nome do movimento, exatamente como no catalogo fechado.</summary>
    public string? Movimento { get; init; }

    public DateTime? DeUtc { get; init; }

    public DateTime? AteUtc { get; init; }
}
