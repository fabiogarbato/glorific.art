namespace Glorific.Domain.ReferenceData;

/// <summary>
/// Chave textual de um movimento de estoque. E um struct e nao string solta para o compilador
/// impedir passar um nome de categoria onde se espera um tipo de movimento — o erro classico de
/// lookup resolvido por string em todo lugar.
/// O valor casa exatamente com a coluna nome da tabela movimentos_estoque.
/// </summary>
public readonly record struct MovimentoEstoqueKey(string Value)
{
    public override string ToString() => Value;

    public static implicit operator string(MovimentoEstoqueKey chave) => chave.Value;
}

/// <summary>
/// Catalogo fechado dos movimentos. O Id inteiro do lookup nunca aparece no codigo de negocio:
/// e resolvido em runtime por chave, com cache em memoria, para o seed poder rodar em qualquer
/// ordem sem congelar identidades.
///
/// Reserva e liberacao existem como movimento proprio de sinal zero: elas nao mexem no estoque
/// fisico, so no reservado, e sem registra-las o ledger nao explica por que o disponivel caiu.
/// </summary>
public static class MovimentoEstoqueKeys
{
    /// <summary>Entrada. Primeira carga da variacao no sistema.</summary>
    public static readonly MovimentoEstoqueKey CadastroInicial = new("Cadastro inicial");

    /// <summary>Entrada. Compra de reposicao junto ao fornecedor ou producao.</summary>
    public static readonly MovimentoEstoqueKey Reabastecimento = new("Reabastecimento");

    /// <summary>Neutro no fisico: incrementa apenas a quantidade reservada.</summary>
    public static readonly MovimentoEstoqueKey ReservaCheckout = new("Reserva de checkout");

    /// <summary>Neutro no fisico: devolve a reserva de um pagamento expirado ou cancelado.</summary>
    public static readonly MovimentoEstoqueKey LiberacaoReserva = new("Liberacao de reserva");

    /// <summary>Saida. Pagamento confirmado: baixa o fisico e zera a reserva correspondente.</summary>
    public static readonly MovimentoEstoqueKey VendaSistema = new("Venda por sistema");

    /// <summary>Saida. Venda registrada fora da loja (feira, encomenda), lancada pelo admin.</summary>
    public static readonly MovimentoEstoqueKey VendaManual = new("Venda manual");

    /// <summary>Entrada. Peca voltou do cliente e foi aprovada para revenda.</summary>
    public static readonly MovimentoEstoqueKey DevolucaoCliente = new("Devolucao de cliente");

    /// <summary>Entrada ou saida. Correcao de contagem apos inventario fisico.</summary>
    public static readonly MovimentoEstoqueKey AjusteInventario = new("Ajuste de inventario");

    /// <summary>Saida. Peca danificada, extraviada ou descartada.</summary>
    public static readonly MovimentoEstoqueKey PerdaAvaria = new("Perda/avaria");

    /// <summary>Ordem estavel para o seeder e para o filtro do painel.</summary>
    public static readonly IReadOnlyList<MovimentoEstoqueKey> Todos =
    [
        CadastroInicial,
        Reabastecimento,
        ReservaCheckout,
        LiberacaoReserva,
        VendaSistema,
        VendaManual,
        DevolucaoCliente,
        AjusteInventario,
        PerdaAvaria
    ];
}
