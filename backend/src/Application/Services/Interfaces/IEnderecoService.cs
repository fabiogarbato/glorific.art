using Glorific.Application.DTO.Conta;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Enderecos de entrega do cliente.
///
/// TODO metodo recebe o uuid do dono, vindo do TOKEN. Nao existe sobrecarga "so pelo id do
/// endereco", e isso e deliberado: a assinatura torna impossivel escrever por distracao o
/// caminho que devolve o endereco de outra pessoa. Quando o endereco existe mas e de outro
/// usuario, a resposta e 404 e nao 403 — 403 confirmaria que aquele id existe.
///
/// Consulta de CEP nao mora aqui: o front chama o ViaCEP e envia o endereco ja preenchido.
/// Este servico so valida e normaliza o que chegou.
/// </summary>
public interface IEnderecoService
{
    /// <summary>Sem paginacao: e a lista de enderecos de UMA pessoa, nao uma tabela.</summary>
    Task<IReadOnlyList<EnderecoResponseDto>> ListarAsync(
        string uuidUsuario,
        CancellationToken cancellationToken = default);

    Task<EnderecoResponseDto> ObterAsync(
        string uuidUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default);

    /// <summary>O primeiro endereco do cliente vira principal automaticamente.</summary>
    Task<EnderecoResponseDto> CriarAsync(
        string uuidUsuario,
        EnderecoCreateDto dto,
        CancellationToken cancellationToken = default);

    Task<EnderecoResponseDto> AtualizarAsync(
        string uuidUsuario,
        int idEndereco,
        EnderecoUpdateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete. Pedido antigo guarda o proprio snapshot de endereco, mas apagar a linha de
    /// verdade quebraria a lista do cliente e qualquer relatorio que ainda a referencie.
    /// </summary>
    Task RemoverAsync(string uuidUsuario, int idEndereco, CancellationToken cancellationToken = default);

    Task<EnderecoResponseDto> DefinirPrincipalAsync(
        string uuidUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default);
}
