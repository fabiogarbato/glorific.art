using Glorific.Application.Common;
using Glorific.Application.DTO.Promocoes;
using Glorific.Domain.Entities.Promocoes;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// CRUD administrativo do cupom mais as duas operacoes que o checkout consome.
///
/// A separacao entre ValidarAsync e ConsumirAsync e deliberada e e o coracao desta area:
/// validar e leitura pura, pode ser chamada a cada tecla digitada no carrinho e nao altera nada;
/// consumir escreve o contador com UPDATE condicional e so pode acontecer uma vez, dentro da
/// transacao do checkout.
///
/// As duas devolvem Resultado em vez de lancar excecao porque cupom recusado e caminho previsivel
/// — o carrinho quer exibir "cupom expirado" sem que isso vire 400 e derrube o resto da resposta.
/// </summary>
public interface ICupomService : IGenericService<Cupom, CupomCreateDto, CupomUpdateDto, CupomResponseDto>
{
    /// <summary>Busca pelo codigo ja normalizado em maiusculas. Lanca 404 quando nao existe.</summary>
    Task<CupomResponseDto> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    /// <summary>Listagem do painel com busca por codigo/descricao e filtro de ativo.</summary>
    Task<PagedResult<CupomResponseDto>> ListarAdminAsync(
        string? busca,
        bool? ativo,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Ledger de usos do cupom: quem usou, em qual pedido e quanto foi descontado.</summary>
    Task<PagedResult<CupomUsoResponseDto>> ListarUsosAsync(
        int idCupom,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica todas as regras (vigencia, valor minimo, teto, uso total, uso por usuario, primeira
    /// compra, restricao de categoria/colecao) e calcula o desconto. NAO escreve nada.
    /// </summary>
    Task<Resultado<CupomAplicadoDto>> ValidarAsync(
        CupomValidacaoRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida e, se passar, consome UM uso do cupom por UPDATE condicional atomico. Falha aqui com
    /// codigo "cupom_esgotado" significa que outro checkout levou o ultimo uso entre a validacao e
    /// a escrita — e exatamente a corrida que este metodo existe para fechar.
    ///
    /// Quem chama e responsavel por DevolverUsoAsync se o checkout abortar depois deste ponto,
    /// a menos que tudo esteja dentro de uma transacao que sera revertida.
    /// </summary>
    Task<Resultado<CupomAplicadoDto>> ConsumirAsync(
        CupomValidacaoRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a linha do ledger. NAO chama SaveChanges de proposito: o registro do uso pertence a
    /// mesma unidade de trabalho do pedido, e o unico (id_cupom, id_pedido) do banco so protege
    /// contra retentativa se as duas escritas commitarem juntas.
    /// </summary>
    Task RegistrarUsoAsync(
        int idCupom,
        int idUsuario,
        int idPedido,
        int valorDescontadoCentavos,
        CancellationToken cancellationToken = default);

    /// <summary>Compensacao de ConsumirAsync quando o checkout falha fora de transacao.</summary>
    Task DevolverUsoAsync(int idCupom, CancellationToken cancellationToken = default);
}
