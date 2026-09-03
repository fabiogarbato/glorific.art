using Glorific.Application.Common;
using Glorific.Application.DTO.Social;
using Glorific.Domain.Enums;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Avaliacao de produto com moderacao previa.
///
/// Nao herda IGenericService de proposito. O CRUD generico traria "atualizar" e "remover" pela
/// porta da frente, e nenhuma das duas existe aqui: avaliacao publicada nao e editada pelo autor
/// (o texto que o moderador aprovou seria trocado depois) nem apagada pelo painel (a nota
/// denormalizada do produto e o historico dependem da linha). O ciclo de vida e outro:
/// nasce Pendente, vira Aprovada ou Rejeitada, e para.
///
/// Os dois DTOs de saida tambem sao intencionais: o publico esconde e-mail e nome completo,
/// o administrativo precisa dos dois para o moderador decidir.
/// </summary>
public interface IAvaliacaoService
{
    /// <summary>Somente APROVADAS. A vitrine nunca ve avaliacao pendente nem rejeitada.</summary>
    Task<PagedResult<AvaliacaoResponseDto>> ListarDoProdutoAsync(
        int idProduto,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Media, distribuicao por nota, percentual de recomendacao e caimento predominante — tudo
    /// agregado no banco. E o bloco que a pagina de produto usa para dizer "veste pequeno".
    /// </summary>
    Task<AvaliacaoResumoDto> ObterResumoDoProdutoAsync(
        int idProduto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria a avaliacao como Pendente. Exige compra do proprio usuario para aquele produto e
    /// aceita uma unica avaliacao por produto por usuario.
    /// </summary>
    Task<AvaliacaoResponseDto> CriarAsync(
        int idUsuario,
        AvaliacaoCreateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Fila de moderacao. Sem status informado, devolve as pendentes, da mais antiga.</summary>
    Task<PagedResult<AvaliacaoAdminResponseDto>> ListarParaModeracaoAsync(
        StatusAvaliacao? status,
        PageRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Aprova e recalcula NotaMedia/TotalAvaliacoes do produto.</summary>
    Task<AvaliacaoAdminResponseDto> AprovarAsync(
        int idAvaliacao,
        int idModerador,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejeita com motivo obrigatorio e recalcula as notas do produto — necessario tambem aqui,
    /// porque rejeitar pode estar derrubando uma avaliacao que ja estava aprovada.
    /// </summary>
    Task<AvaliacaoAdminResponseDto> RejeitarAsync(
        int idAvaliacao,
        int idModerador,
        string motivo,
        CancellationToken cancellationToken = default);
}
