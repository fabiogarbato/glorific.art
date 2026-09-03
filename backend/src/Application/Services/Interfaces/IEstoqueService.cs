using Glorific.Application.Common;
using Glorific.Application.DTO.Estoque;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Estoque por SKU: reserva, liberacao, efetivacao e as operacoes de painel.
///
/// LEIA ANTES DE USAR — a divisao mais importante deste contrato:
///
/// 1. PRIMITIVAS TRANSACIONAIS (Reservar, LiberarReserva, EfetivarVenda, DevolverAoEstoque)
///    NAO chamam SaveChanges. Elas rodam DENTRO da transacao de quem as chamou — o checkout
///    reserva estoque, consome cupom e cria o pedido, e ou tudo acontece ou nada acontece.
///    O UPDATE de saldo em si ja e atomico no banco (ExecuteUpdate condicional, imediato); o
///    que fica pendente de commit e a linha do ledger. Salvar aqui dentro quebraria a
///    atomicidade do caso de uso chamador.
///
/// 2. CASOS DE USO DO PAINEL (RegistrarEntrada, Ajustar, AtualizarParametros) sao completos e
///    COMMITAM: chegam de um endpoint administrativo e nao compoem nada maior.
///
/// As primitivas devolvem <see cref="Resultado"/> e nao lancam: falta de saldo e resposta de
/// negocio esperada dentro de um laco de itens, e lancar no primeiro item perderia os demais.
/// Quem estiver na fronteira do caso de uso chama LancarSeFalhou para virar 400.
///
/// TODA operacao grava MovimentacaoEstoque com QuantidadeAntes e QuantidadeDepois. Sem isso o
/// inventario nao fecha e ninguem consegue responder onde a peca sumiu.
/// </summary>
public interface IEstoqueService
{
    /// <summary>
    /// Reserva SOFT para checkout: incrementa quantidade_reservada, nao mexe no fisico.
    /// Falha (sem lancar) quando nao ha saldo disponivel — com a mensagem ja pronta no formato
    /// "Tamanho M em Terracota esgotado".
    /// </summary>
    Task<Resultado> ReservarAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        CancellationToken cancellationToken = default);

    /// <summary>Devolve reserva de pagamento expirado, recusado ou cancelado. Nao toca no fisico.</summary>
    Task<Resultado> LiberarReservaAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        string? observacao = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pagamento confirmado: baixa fisico e reserva na mesma instrucao atomica e grava
    /// "Venda por sistema". Chamado pelo processamento do webhook.
    /// </summary>
    Task<Resultado> EfetivarVendaAsync(
        int idVariacao,
        int quantidade,
        int idPedido,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolucao de cliente aprovada: entrada no fisico com o movimento proprio.
    /// Separado de RegistrarEntrada porque o relatorio precisa distinguir peca que voltou de
    /// peca comprada do fornecedor.
    /// </summary>
    Task<Resultado> DevolverAoEstoqueAsync(
        int idVariacao,
        int quantidade,
        int? idPedido = null,
        int? idUsuario = null,
        string? observacao = null,
        CancellationToken cancellationToken = default);

    /// <summary>Entrada em lote (nota do fornecedor). Caso de uso completo: commita.</summary>
    Task<IReadOnlyList<EstoqueVariacaoResponseDto>> RegistrarEntradaAsync(
        EstoqueEntradaDto dto,
        string? uuidUsuario,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajuste de inventario pela contagem fisica encontrada. Caso de uso completo: commita.
    /// Ajuste negativo respeita reserva alheia: nao e possivel derrubar pedido ja pago.
    /// </summary>
    Task<EstoqueVariacaoResponseDto> AjustarAsync(
        EstoqueAjusteDto dto,
        string? uuidUsuario,
        CancellationToken cancellationToken = default);

    /// <summary>Minimo de alerta e localizacao fisica. Nao mexe em saldo.</summary>
    Task<EstoqueVariacaoResponseDto> AtualizarParametrosAsync(
        int idVariacao,
        EstoqueParametrosUpdateDto dto,
        CancellationToken cancellationToken = default);

    Task<EstoqueVariacaoResponseDto> ObterPorVariacaoAsync(
        int idVariacao,
        CancellationToken cancellationToken = default);

    /// <summary>Relatorio de reposicao: disponivel abaixo do minimo configurado.</summary>
    Task<IReadOnlyList<EstoqueVariacaoResponseDto>> ObterAbaixoDoMinimoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Extrato do ledger, paginado. O ledger cresce para sempre e nunca vem inteiro.</summary>
    Task<PagedResult<MovimentacaoEstoqueResponseDto>> ListarMovimentacoesAsync(
        MovimentacaoEstoqueFiltro filtro,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);
}
