using Glorific.Application.Common;
using Glorific.Application.DTO.Identidade;
using Glorific.Application.Exceptions;
using Glorific.Application.Mappings;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Glorific.Application.Services;

/// <summary>
/// Perfil do proprio usuario e administracao de usuarios.
///
/// A separacao entre os dois blocos e o que importa aqui: o que o cliente chama e chaveado por
/// UUID vindo do token, e o que o admin chama e chaveado por Id de rota. Nao existe metodo
/// publico que aceite Id de rota em nome do cliente — e a assinatura, e nao um if la dentro,
/// que impede o IDOR classico de trocar o numero da URL e ler o perfil alheio.
/// </summary>
public sealed class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IConsultaAssincrona _consulta;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(
        IUsuarioRepository usuarios,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        IConsultaAssincrona consulta,
        IUnitOfWork unitOfWork,
        IClock relogio,
        ILogger<UsuarioService> logger)
    {
        _usuarios = usuarios;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _consulta = consulta;
        _unitOfWork = unitOfWork;
        _relogio = relogio;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Do proprio usuario
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> ObterPerfilAsync(
        string uuidUsuario,
        CancellationToken cancellationToken = default)
    {
        var dto = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.Query().Where(u => u.Uuid == uuidUsuario).Select(UsuarioProjecao.Resposta),
            cancellationToken);

        // Token assinado por nos apontando para usuario inexistente: a sessao e que esta errada.
        return dto ?? throw new UnauthorizedAccessException("Sessao expirada. Entre novamente.");
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> AtualizarPerfilAsync(
        string uuidUsuario,
        PerfilUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.QueryTracked().Where(u => u.Uuid == uuidUsuario),
            cancellationToken)
            ?? throw new UnauthorizedAccessException("Sessao expirada. Entre novamente.");

        if (!usuario.Ativo)
            throw new BusinessValidationException("Esta conta esta desativada. Fale com o atendimento.");

        await AplicarDadosPessoaisAsync(
            usuario,
            dto.NomeCompleto,
            dto.Telefone,
            dto.Cpf,
            dto.AceitaMarketing,
            cancellationToken);

        usuario.DataNascimento = ValidarNascimento(dto.DataNascimento);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Administrativo
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<UsuarioResponseDto>> ListarAsync(
        PageRequest requisicao,
        string? busca = null,
        string? papel = null,
        bool? ativo = null,
        CancellationToken cancellationToken = default)
    {
        requisicao ??= new PageRequest();

        var consulta = _usuarios.Query();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // E-mail ja esta gravado em minusculas; o nome precisa do ToLower para o LIKE nao
            // depender de como a pessoa digitou.
            var termo = busca.Trim().ToLowerInvariant();

            consulta = consulta.Where(u =>
                u.Email.Contains(termo) ||
                (u.NomeCompleto != null && u.NomeCompleto.ToLower().Contains(termo)) ||
                (u.Cpf != null && u.Cpf.Contains(termo)));
        }

        if (!string.IsNullOrWhiteSpace(papel))
        {
            var nomePapel = papel.Trim().ToLowerInvariant();
            consulta = consulta.Where(u => u.Roles.Any(vinculo => vinculo.Role.Nome == nomePapel));
        }

        if (ativo is not null)
            consulta = consulta.Where(u => u.Ativo == ativo.Value);

        // COUNT antes do Skip/Take: Total e a contagem no banco, nunca Items.Count.
        var total = await _consulta.ContarAsync(consulta, cancellationToken);

        if (total == 0)
            return PagedResult<UsuarioResponseDto>.Vazio(requisicao.Page, requisicao.PageSize);

        // Ordenacao deterministica com desempate por Id: sem ele, dois cadastros do mesmo
        // instante trocam de lugar entre a pagina 1 e a 2 e uma linha some da listagem.
        var pagina = consulta
            .OrderByDescending(u => u.DataCriacao)
            .ThenByDescending(u => u.Id)
            .Skip(requisicao.Skip)
            .Take(requisicao.Take)
            .Select(UsuarioProjecao.Resposta);

        var itens = await _consulta.ListarAsync(pagina, cancellationToken);

        return PagedResult<UsuarioResponseDto>.Criar(itens, requisicao, total);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var dto = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.Query().Where(u => u.Id == id).Select(UsuarioProjecao.Resposta),
            cancellationToken);

        return dto ?? throw new EntityNotFoundException("Usuario", id);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> AtualizarAsync(
        int id,
        UsuarioAdminUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await ObterParaEdicaoAsync(id, cancellationToken);

        await AplicarDadosPessoaisAsync(
            usuario,
            dto.NomeCompleto,
            dto.Telefone,
            dto.Cpf,
            dto.AceitaMarketing,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> ConcederPapelAsync(
        int idAlvo,
        string papel,
        string uuidSolicitante,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterParaEdicaoAsync(idAlvo, cancellationToken);

        GarantirQueNaoEhEleMesmo(usuario, uuidSolicitante);

        var role = await ObterPapelAsync(papel, cancellationToken);

        var vinculo = await _roles.ObterVinculoAsync(usuario.Id, role.Id, cancellationToken);

        // Idempotente: conceder duas vezes nao pode estourar violacao de PK composta.
        if (vinculo is null)
        {
            var solicitante = await _usuarios.ObterPorUuidAsync(uuidSolicitante, cancellationToken);

            await _roles.ConcederAsync(
                new UsuarioRole
                {
                    IdUsuario = usuario.Id,
                    IdRole = role.Id,
                    ConcedidaEm = _relogio.UtcNow,

                    // Responde "quem promoveu este usuario", que e a pergunta de auditoria mais
                    // cara de responder depois que o estrago aconteceu.
                    ConcedidaPor = solicitante?.Id
                },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Papel {Papel} concedido ao usuario {Uuid} por {Solicitante}.",
                role.Nome, usuario.Uuid, uuidSolicitante);
        }

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> RevogarPapelAsync(
        int idAlvo,
        string papel,
        string uuidSolicitante,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterParaEdicaoAsync(idAlvo, cancellationToken);

        GarantirQueNaoEhEleMesmo(usuario, uuidSolicitante);

        var role = await ObterPapelAsync(papel, cancellationToken);

        var vinculo = await _roles.ObterVinculoAsync(usuario.Id, role.Id, cancellationToken);

        if (vinculo is not null)
        {
            _roles.Revogar(vinculo);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Papel revogado nao expira o access token que ja esta na mao do usuario (ele vale
            // ate 15 min). Derrubar o refresh garante que a proxima renovacao ja venha sem o
            // papel, em vez de a sessao antiga sobreviver por 30 dias com o privilegio velho.
            await _refreshTokens.RevogarDoUsuarioAsync(usuario.Id, _relogio.UtcNow, cancellationToken);

            _logger.LogWarning(
                "Papel {Papel} revogado do usuario {Uuid} por {Solicitante}. Sessoes derrubadas.",
                role.Nome, usuario.Uuid, uuidSolicitante);
        }

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> DesativarAsync(
        int id,
        string uuidSolicitante,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterParaEdicaoAsync(id, cancellationToken);

        // Auto-desativacao tira o proprio admin do ar e, se ele for o unico, tranca a loja.
        GarantirQueNaoEhEleMesmo(usuario, uuidSolicitante);

        if (usuario.Ativo)
        {
            // Soft delete: pedidos e avaliacoes antigos continuam apontando para este usuario.
            usuario.Ativo = false;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Sem isto a conta fica "desativada" com a sessao ainda funcionando por 30 dias.
            await _refreshTokens.RevogarDoUsuarioAsync(usuario.Id, _relogio.UtcNow, cancellationToken);

            _logger.LogWarning(
                "Usuario {Uuid} desativado por {Solicitante}. Sessoes revogadas.",
                usuario.Uuid, uuidSolicitante);
        }

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> AtivarAsync(
        int id,
        string uuidSolicitante,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterParaEdicaoAsync(id, cancellationToken);

        if (!usuario.Ativo)
        {
            usuario.Ativo = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Usuario {Uuid} reativado por {Solicitante}.", usuario.Uuid, uuidSolicitante);
        }

        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private async Task<Usuario> ObterParaEdicaoAsync(int id, CancellationToken cancellationToken) =>
        await _usuarios.ObterParaEdicaoAsync(id, cancellationToken)
        ?? throw new EntityNotFoundException("Usuario", id);

    private async Task<Role> ObterPapelAsync(string papel, CancellationToken cancellationToken)
    {
        var nome = (papel ?? string.Empty).Trim().ToLowerInvariant();

        // Papel e linha de tabela: recusar nome desconhecido evita gravar um vinculo para um
        // papel que nenhuma policy conhece e que, portanto, nao protege nada.
        if (!Domain.Constants.Roles.Todos.Contains(nome))
            throw new BusinessValidationException($"Papel '{papel}' nao existe.");

        return await _roles.ObterPorNomeAsync(nome, cancellationToken)
            ?? throw new BusinessValidationException(
                $"Papel '{nome}' nao esta cadastrado. O seed inicial de papeis nao rodou neste banco.");
    }

    /// <summary>
    /// Fecha o caminho de auto-escalonamento: ninguem altera as proprias permissoes nem se
    /// desativa. Sem isto, qualquer conta com acesso ao painel vira admin em um clique.
    /// </summary>
    private static void GarantirQueNaoEhEleMesmo(Usuario alvo, string uuidSolicitante)
    {
        if (string.Equals(alvo.Uuid, uuidSolicitante, StringComparison.Ordinal))
            throw new BusinessValidationException(
                "Nao e possivel alterar as proprias permissoes ou desativar a propria conta.");
    }

    private async Task AplicarDadosPessoaisAsync(
        Usuario usuario,
        string? nomeCompleto,
        string? telefone,
        string? cpf,
        bool aceitaMarketing,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(nomeCompleto))
            usuario.NomeCompleto = nomeCompleto.Trim();

        var telefoneDigitos = TelefoneHelper.SomenteDigitos(telefone);

        if (telefoneDigitos.Length > 0)
        {
            if (!TelefoneHelper.Valido(telefoneDigitos))
                throw new BusinessValidationException("Telefone invalido. Informe DDD e numero.");

            usuario.Telefone = telefoneDigitos;
        }
        else
        {
            usuario.Telefone = null;
        }

        var cpfDigitos = DocumentoHelper.SomenteDigitos(cpf);

        if (cpfDigitos.Length > 0)
        {
            if (!DocumentoHelper.CpfValido(cpfDigitos))
                throw new BusinessValidationException("CPF invalido.");

            // O indice de CPF e unico parcial: colidir aqui viraria erro de driver em 500.
            if (await _usuarios.CpfEmUsoAsync(cpfDigitos, usuario.Id, cancellationToken))
                throw new BusinessValidationException("Este CPF ja esta em uso por outra conta.");

            usuario.Cpf = cpfDigitos;
        }
        else
        {
            usuario.Cpf = null;
        }

        usuario.AceitaMarketing = aceitaMarketing;
    }

    /// <summary>
    /// Data de nascimento no futuro e erro de digitacao, e idade absurda tambem. Barrar aqui
    /// evita que o relatorio de aniversariantes vire lixo silencioso.
    /// </summary>
    private DateTime? ValidarNascimento(DateTime? nascimento)
    {
        if (nascimento is null)
            return null;

        var data = nascimento.Value.Date;
        var hoje = _relogio.UtcNow.Date;

        if (data > hoje)
            throw new BusinessValidationException("Data de nascimento nao pode estar no futuro.");

        if (data < hoje.AddYears(-120))
            throw new BusinessValidationException("Data de nascimento invalida.");

        return data;
    }
}
