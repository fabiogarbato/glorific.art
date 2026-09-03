using Glorific.Application.Common;
using Glorific.Application.DTO.Identidade;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Perfil do proprio usuario e administracao de usuarios.
///
/// Nao herda IGenericService de proposito. O CRUD generico e chaveado por Id inteiro e nao
/// conhece dono: expor ObterPorIdAsync para o cliente final seria IDOR direto, qualquer um
/// lendo o perfil de qualquer outro trocando o numero da URL. Aqui os metodos do cliente sao
/// chaveados por UUID vindo do TOKEN e os metodos por Id existem so atras da policy de admin.
/// </summary>
public interface IUsuarioService
{
    // ---------- Do proprio usuario (uuid vem do token, nunca da rota) ----------

    Task<UsuarioResponseDto> ObterPerfilAsync(string uuidUsuario, CancellationToken cancellationToken = default);

    Task<UsuarioResponseDto> AtualizarPerfilAsync(
        string uuidUsuario,
        PerfilUpdateDto dto,
        CancellationToken cancellationToken = default);

    // ---------- Administrativo (policy SomenteAdmin) ----------

    /// <summary>Listagem paginada com filtro por texto, papel e situacao.</summary>
    Task<PagedResult<UsuarioResponseDto>> ListarAsync(
        PageRequest requisicao,
        string? busca = null,
        string? papel = null,
        bool? ativo = null,
        CancellationToken cancellationToken = default);

    Task<UsuarioResponseDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<UsuarioResponseDto> AtualizarAsync(
        int id,
        UsuarioAdminUpdateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Concede um papel. Alterar os PROPRIOS papeis e bloqueado: e o caminho de
    /// auto-escalonamento e tambem o de auto-rebaixamento acidental que deixa a loja sem admin.
    /// </summary>
    Task<UsuarioResponseDto> ConcederPapelAsync(
        int idAlvo,
        string papel,
        string uuidSolicitante,
        CancellationToken cancellationToken = default);

    Task<UsuarioResponseDto> RevogarPapelAsync(
        int idAlvo,
        string papel,
        string uuidSolicitante,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete: marca inativo e derruba todas as sessoes do usuario.</summary>
    Task<UsuarioResponseDto> DesativarAsync(
        int id,
        string uuidSolicitante,
        CancellationToken cancellationToken = default);

    Task<UsuarioResponseDto> AtivarAsync(
        int id,
        string uuidSolicitante,
        CancellationToken cancellationToken = default);
}
