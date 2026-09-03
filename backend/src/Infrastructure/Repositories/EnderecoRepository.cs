using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Infrastructure.Repositories;

public sealed class EnderecoRepository : BaseRepository<Endereco>, IEnderecoRepository
{
    public EnderecoRepository(GlorificContext contexto) : base(contexto)
    {
    }

    public async Task<IReadOnlyList<Endereco>> ObterDoUsuarioAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        await Query()
            .Where(e => e.IdUsuario == idUsuario && e.Ativo)
            .OrderByDescending(e => e.Principal)
            .ThenByDescending(e => e.DataCriacao)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Sempre filtrando por usuario.
    ///
    /// Buscar so por Id abriria IDOR: o cliente enviaria no checkout o Id do endereco de outra
    /// pessoa e a etiqueta sairia com o endereco dela. O dono entra no WHERE, nao em um if
    /// depois de carregar.
    /// </summary>
    public Task<Endereco?> ObterDoUsuarioAsync(
        int idUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(
            e => e.Id == idEndereco && e.IdUsuario == idUsuario,
            cancellationToken);

    public Task<Endereco?> ObterPrincipalAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(
            e => e.IdUsuario == idUsuario && e.Principal && e.Ativo,
            cancellationToken);

    /// <summary>
    /// UPDATE em bloco antes de marcar o novo principal: so pode existir um por usuario.
    ///
    /// Carregar todos e alterar um a um faria N updates e, pior, deixaria dois principais se
    /// duas abas do cliente salvassem ao mesmo tempo. Aqui e uma instrucao so.
    ///
    /// ExecuteUpdateAsync nao mexe no identity map: as instancias que AINDA se acham principais
    /// sao desanexadas depois, senao um SaveChanges posterior gravaria Principal = true de novo
    /// a partir do valor velho em memoria. O endereco que o caso de uso vai promover continua
    /// rastreado de proposito — ele estava com Principal = false e nao foi tocado pelo UPDATE.
    /// </summary>
    public async Task DesmarcarPrincipaisAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        await Contexto.Enderecos
            .Where(e => e.IdUsuario == idUsuario && e.Principal)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Principal, false),
                cancellationToken);

        DesanexarRastreados<Endereco>(e => e.IdUsuario == idUsuario && e.Principal);
    }
}
