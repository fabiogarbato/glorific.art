using Glorific.Application.Common;
using Glorific.Application.DTO.Identidade;
using Glorific.Application.Exceptions;
using Glorific.Application.Mappings;
using Glorific.Application.Models.Auth;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Application.Services;

/// <summary>
/// Todo o ciclo de sessao: cadastro, login por senha, login por Google, rotacao de refresh,
/// logout e senha esquecida.
///
/// Quatro regras duras estao materializadas aqui e nao devem ser afrouxadas:
/// 1. Papel vem SEMPRE de usuarios_roles. Nenhum caminho — nem o corpo da requisicao, nem o
///    payload do Google — influencia a claim role.
/// 2. Reapresentar um refresh token ja substituido revoga a familia INTEIRA. E o que transforma
///    roubo de refresh em incidente detectado, em vez de acesso permanente e silencioso.
/// 3. Trocar ou redefinir senha derruba as sessoes existentes. Sem isso, "troquei a senha
///    porque desconfiei" nao expulsa quem estava dentro.
/// 4. Nenhum "agora" sai de DateTime.UtcNow: tudo vem de IClock.
/// </summary>
public sealed class AutenticacaoService : IAutenticacaoService
{
    private const string CredencialInvalida = "E-mail ou senha invalidos.";
    private const string SessaoInvalida = "Sessao expirada. Entre novamente.";
    private const string LinkInvalido = "Link de redefinicao invalido ou expirado. Peca um novo.";

    /// <summary>Janela do link de redefinicao. Curta: e um link que troca senha por e-mail.</summary>
    private const int MinutosLinkRedefinicao = 30;

    private readonly IUsuarioRepository _usuarios;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IConsultaAssincrona _consulta;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;
    private readonly ITokenService _tokens;
    private readonly IGoogleTokenValidator _google;
    private readonly ITokenRedefinicaoSenha _tokenRedefinicao;
    private readonly IEmailSender _email;
    private readonly JwtOptions _jwt;
    private readonly GoogleOptions _googleOpcoes;
    private readonly AppOptions _app;
    private readonly ILogger<AutenticacaoService> _logger;

    public AutenticacaoService(
        IUsuarioRepository usuarios,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        IConsultaAssincrona consulta,
        IUnitOfWork unitOfWork,
        IClock relogio,
        ITokenService tokens,
        IGoogleTokenValidator google,
        ITokenRedefinicaoSenha tokenRedefinicao,
        IEmailSender email,
        IOptions<JwtOptions> jwt,
        IOptions<GoogleOptions> googleOpcoes,
        IOptions<AppOptions> app,
        ILogger<AutenticacaoService> logger)
    {
        _usuarios = usuarios;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _consulta = consulta;
        _unitOfWork = unitOfWork;
        _relogio = relogio;
        _tokens = tokens;
        _google = google;
        _tokenRedefinicao = tokenRedefinicao;
        _email = email;
        _jwt = jwt.Value;
        _googleOpcoes = googleOpcoes.Value;
        _app = app.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Cadastro e login por senha
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<SessaoAutenticada> RegistrarAsync(
        RegistroRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizarEmail(dto.Email);
        var cpf = DocumentoHelper.SomenteDigitos(dto.Cpf);
        var telefone = TelefoneHelper.SomenteDigitos(dto.Telefone);

        if (await _usuarios.EmailEmUsoAsync(email, null, cancellationToken))
            throw new BusinessValidationException("Ja existe uma conta com este e-mail.");

        if (cpf.Length > 0)
        {
            if (!DocumentoHelper.CpfValido(cpf))
                throw new BusinessValidationException("CPF invalido.");

            if (await _usuarios.CpfEmUsoAsync(cpf, null, cancellationToken))
                throw new BusinessValidationException("Ja existe uma conta com este CPF.");
        }

        if (telefone.Length > 0 && !TelefoneHelper.Valido(telefone))
            throw new BusinessValidationException("Telefone invalido. Informe DDD e numero.");

        var agora = _relogio.UtcNow;

        var usuario = new Usuario
        {
            Uuid = Guid.NewGuid().ToString(),
            Email = email,
            EmailVerificado = false,
            NomeCompleto = Truncar(dto.NomeCompleto.Trim(), 180),
            Cpf = cpf.Length == 0 ? null : cpf,
            Telefone = telefone.Length == 0 ? null : telefone,
            SenhaHash = Senhas.Hash(dto.Senha),
            AceitaMarketing = dto.AceitaMarketing,
            Ativo = true,
            UltimoLoginEm = agora
        };

        // Transacao explicita porque o cadastro precisa de dois SaveChanges: o primeiro gera o
        // Id do usuario, e o vinculo de papel e a linha de refresh dependem desse Id. Sem a
        // transacao, uma falha no meio deixa um usuario SEM papel nenhum — conta que existe,
        // loga e nao consegue fazer nada, e ninguem descobre ate o cliente reclamar.
        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        await _usuarios.AdicionarAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ConcederPapelAsync(usuario.Id, Domain.Constants.Roles.Cliente, cancellationToken);

        var sessao = await EmitirSessaoAsync(
            usuario,
            [Domain.Constants.Roles.Cliente],
            Guid.NewGuid(),
            origem,
            null,
            cancellationToken);

        await transacao.CommitAsync(cancellationToken);

        _logger.LogInformation("Cadastro concluido. Usuario {Uuid}.", usuario.Uuid);

        return sessao;
    }

    /// <inheritdoc />
    public async Task<SessaoAutenticada> LoginAsync(
        LoginRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizarEmail(dto.Email);

        var usuario = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.QueryTracked().Where(u => u.Email == email),
            cancellationToken);

        // Conta inexistente, conta so-Google, conta desativada e senha errada saem pela MESMA
        // porta, com o MESMO custo de tempo. Qualquer diferenca aqui responde "este e-mail tem
        // conta na loja?" para quem so quer descobrir isso.
        if (usuario is null || string.IsNullOrEmpty(usuario.SenhaHash) || !usuario.Ativo)
        {
            Senhas.Equalizar(dto.Senha);
            throw new UnauthorizedAccessException(CredencialInvalida);
        }

        if (!Senhas.Confere(dto.Senha, usuario.SenhaHash))
            throw new UnauthorizedAccessException(CredencialInvalida);

        usuario.UltimoLoginEm = _relogio.UtcNow;

        var papeis = await _roles.ObterNomesDoUsuarioAsync(usuario.Id, cancellationToken);

        return await EmitirSessaoAsync(usuario, papeis, Guid.NewGuid(), origem, null, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Login com Google
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<SessaoAutenticada> LoginGoogleAsync(
        GoogleLoginRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var identidade = await ValidarGoogleAsync(dto.IdToken, cancellationToken);
        var email = NormalizarEmail(identidade.Email);
        var agora = _relogio.UtcNow;

        // 1) Caminho normal: ja existe vinculo (provedor, sub). O sub e imutavel; o e-mail nao.
        var vinculo = await _usuarios.ObterLoginExternoAsync(
            ProvedoresLoginExterno.Google, identidade.Subject, cancellationToken);

        if (vinculo is not null)
        {
            var usuarioVinculado = await _usuarios.ObterParaEdicaoAsync(vinculo.IdUsuario, cancellationToken)
                ?? throw new EntityNotFoundException("Usuario", vinculo.IdUsuario);

            GarantirAtivo(usuarioVinculado);

            vinculo.UltimoUsoEm = agora;
            vinculo.EmailNoProvedor = email;

            AplicarPerfilGoogle(usuarioVinculado, identidade, agora);

            var papeisVinculado = await _roles.ObterNomesDoUsuarioAsync(usuarioVinculado.Id, cancellationToken);

            return await EmitirSessaoAsync(
                usuarioVinculado, papeisVinculado, Guid.NewGuid(), origem, null, cancellationToken);
        }

        // 2) Sem vinculo: casa por e-mail. So e seguro porque o Google confirmou EmailVerificado
        //    logo acima — vincular por e-mail nao verificado permitiria tomar a conta de outra
        //    pessoa apenas criando uma conta Google com o mesmo endereco.
        var existente = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.QueryTracked().Where(u => u.Email == email),
            cancellationToken);

        if (existente is not null)
        {
            GarantirAtivo(existente);

            await _usuarios.AdicionarLoginExternoAsync(
                NovoLoginExterno(existente.Id, identidade, email, agora), cancellationToken);

            // O Google acabou de provar a posse do endereco.
            existente.EmailVerificado = true;
            AplicarPerfilGoogle(existente, identidade, agora);

            var papeisExistente = await _roles.ObterNomesDoUsuarioAsync(existente.Id, cancellationToken);

            _logger.LogInformation("Conta Google vinculada a usuario existente {Uuid}.", existente.Uuid);

            return await EmitirSessaoAsync(
                existente, papeisExistente, Guid.NewGuid(), origem, null, cancellationToken);
        }

        // 3) Conta nova, sem senha. Papel SEMPRE cliente: nada no payload do Google decide isso.
        var novo = new Usuario
        {
            Uuid = Guid.NewGuid().ToString(),
            Email = email,
            EmailVerificado = true,
            NomeCompleto = Truncar(identidade.Nome?.Trim(), 180),
            FotoUrl = identidade.FotoUrl,
            SenhaHash = null,
            Ativo = true,
            UltimoLoginEm = agora
        };

        await using var transacao = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        await _usuarios.AdicionarAsync(novo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _usuarios.AdicionarLoginExternoAsync(
            NovoLoginExterno(novo.Id, identidade, email, agora), cancellationToken);

        await ConcederPapelAsync(novo.Id, Domain.Constants.Roles.Cliente, cancellationToken);

        var sessaoNova = await EmitirSessaoAsync(
            novo,
            [Domain.Constants.Roles.Cliente],
            Guid.NewGuid(),
            origem,
            null,
            cancellationToken);

        await transacao.CommitAsync(cancellationToken);

        _logger.LogInformation("Conta criada por login Google. Usuario {Uuid}.", novo.Uuid);

        return sessaoNova;
    }

    /// <inheritdoc />
    public async Task<UsuarioResponseDto> VincularGoogleAsync(
        string uuidUsuario,
        GoogleLoginRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await ObterTrackedPorUuidAsync(uuidUsuario, cancellationToken);
        GarantirAtivo(usuario);

        var identidade = await ValidarGoogleAsync(dto.IdToken, cancellationToken);
        var email = NormalizarEmail(identidade.Email);
        var agora = _relogio.UtcNow;

        var vinculo = await _usuarios.ObterLoginExternoAsync(
            ProvedoresLoginExterno.Google, identidade.Subject, cancellationToken);

        if (vinculo is not null)
        {
            // Deixar passar em silencio aqui seria dizer "vinculado!" para uma conta Google que
            // na verdade abre a sessao de OUTRA pessoa.
            if (vinculo.IdUsuario != usuario.Id)
                throw new BusinessValidationException("Esta conta Google ja esta vinculada a outro usuario.");

            vinculo.UltimoUsoEm = agora;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await ObterDtoAsync(usuario.Id, cancellationToken);
        }

        await _usuarios.AdicionarLoginExternoAsync(
            NovoLoginExterno(usuario.Id, identidade, email, agora), cancellationToken);

        // Vinculo com e-mail diferente do da conta e legitimo (a pessoa tem dois enderecos), mas
        // nao promove verificacao: o Google verificou o e-mail DELE, nao o cadastrado aqui.
        if (string.Equals(usuario.Email, email, StringComparison.Ordinal))
            usuario.EmailVerificado = true;

        AplicarPerfilGoogle(usuario, identidade, agora: null);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterDtoAsync(usuario.Id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Refresh, rotacao e logout
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<SessaoAutenticada> RenovarAsync(
        string? refreshTokenClaro,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenClaro))
            throw new UnauthorizedAccessException(SessaoInvalida);

        var agora = _relogio.UtcNow;

        // Busca pelo hash: o valor em claro nunca vai ao banco e a comparacao acontece no
        // indice unico, nao em memoria depois de carregar linhas.
        var atual = await _refreshTokens.ObterPorHashAsync(
            _tokens.HashRefreshToken(refreshTokenClaro), cancellationToken);

        if (atual is null)
            throw new UnauthorizedAccessException(SessaoInvalida);

        // DETECCAO DE REUSO: revogado E com sucessor registrado significa que este token ja foi
        // trocado uma vez. Se ele reapareceu, alguem tem uma copia — e o legitimo esta com o
        // sucessor. Revogar so esta linha deixaria o atacante seguir usando o sucessor, entao a
        // familia inteira cai e as duas pontas sao forcadas a autenticar de novo.
        if (atual.RevogadoEm is not null && atual.SubstituidoPorHash is not null)
        {
            _logger.LogWarning(
                "Reuso de refresh token detectado. Familia {Familia} do usuario {IdUsuario} revogada. Origem {Ip}.",
                atual.IdFamilia,
                atual.IdUsuario,
                origem.Ip);

            await _refreshTokens.RevogarFamiliaAsync(atual.IdFamilia, agora, cancellationToken);

            throw new UnauthorizedAccessException("Sessao encerrada por seguranca. Entre novamente.");
        }

        if (atual.RevogadoEm is not null || atual.ExpiraEm <= agora)
            throw new UnauthorizedAccessException(SessaoInvalida);

        var usuario = await _usuarios.ObterParaEdicaoAsync(atual.IdUsuario, cancellationToken);

        if (usuario is null || !usuario.Ativo)
        {
            // Conta desativada durante a sessao: a familia morre junto, senao o refresh continua
            // renovando acesso por 30 dias para quem acabou de ser desligado.
            await _refreshTokens.RevogarFamiliaAsync(atual.IdFamilia, agora, cancellationToken);
            throw new UnauthorizedAccessException(SessaoInvalida);
        }

        var papeis = await _roles.ObterNomesDoUsuarioAsync(usuario.Id, cancellationToken);

        // MESMA familia: a sessao continua sendo a mesma, so o token muda.
        return await EmitirSessaoAsync(usuario, papeis, atual.IdFamilia, origem, atual, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string? refreshTokenClaro, CancellationToken cancellationToken = default)
    {
        // Idempotente: cookie ausente, expirado ou desconhecido nao vira erro. Logout que falha
        // e logout que o usuario tenta de novo achando que nao saiu.
        if (string.IsNullOrWhiteSpace(refreshTokenClaro))
            return;

        var atual = await _refreshTokens.ObterPorHashAsync(
            _tokens.HashRefreshToken(refreshTokenClaro), cancellationToken);

        if (atual is null)
            return;

        await _refreshTokens.RevogarFamiliaAsync(atual.IdFamilia, _relogio.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogoutTodosAsync(string uuidUsuario, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObterPorUuidAsync(uuidUsuario, cancellationToken)
            ?? throw new UnauthorizedAccessException(SessaoInvalida);

        var revogados = await _refreshTokens.RevogarDoUsuarioAsync(
            usuario.Id, _relogio.UtcNow, cancellationToken);

        _logger.LogInformation(
            "Logout global do usuario {Uuid}: {Quantidade} sessoes revogadas.", usuario.Uuid, revogados);
    }

    // ------------------------------------------------------------------
    // Senha
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<SessaoAutenticada> TrocarSenhaAsync(
        string uuidUsuario,
        TrocarSenhaRequestDto dto,
        OrigemRequisicao origem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await ObterTrackedPorUuidAsync(uuidUsuario, cancellationToken);
        GarantirAtivo(usuario);

        if (string.IsNullOrEmpty(usuario.SenhaHash))
            throw new BusinessValidationException(
                "Esta conta ainda nao tem senha. Use 'esqueci minha senha' para definir a primeira.");

        if (!Senhas.Confere(dto.SenhaAtual, usuario.SenhaHash))
            throw new BusinessValidationException("Senha atual incorreta.");

        if (Senhas.Confere(dto.NovaSenha, usuario.SenhaHash))
            throw new BusinessValidationException("A nova senha precisa ser diferente da atual.");

        usuario.SenhaHash = Senhas.Hash(dto.NovaSenha);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Depois de persistir a senha nova, TODAS as sessoes caem — inclusive a de quem esta
        // trocando. Ele recebe uma sessao nova logo abaixo; qualquer outro dispositivo (ou
        // invasor) precisa da senha nova para voltar.
        var agora = _relogio.UtcNow;
        await _refreshTokens.RevogarDoUsuarioAsync(usuario.Id, agora, cancellationToken);

        var papeis = await _roles.ObterNomesDoUsuarioAsync(usuario.Id, cancellationToken);

        _logger.LogInformation("Senha alterada. Usuario {Uuid}. Todas as sessoes revogadas.", usuario.Uuid);

        return await EmitirSessaoAsync(usuario, papeis, Guid.NewGuid(), origem, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task EsqueciSenhaAsync(
        EsqueciSenhaRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizarEmail(dto.Email);
        var usuario = await _usuarios.ObterPorEmailAsync(email, cancellationToken);

        // Sai pelo mesmo 204 de quando o e-mail existe. Responder diferente aqui entrega a
        // lista de clientes da loja para quem tiver paciencia de testar enderecos.
        if (usuario is null || !usuario.Ativo)
        {
            _logger.LogInformation("Redefinicao pedida para e-mail sem conta ativa. Resposta permanece 204.");
            return;
        }

        var expiraEm = _relogio.UtcNow.AddMinutes(MinutosLinkRedefinicao);
        var token = _tokenRedefinicao.Gerar(usuario.Uuid, usuario.SenhaHash, expiraEm);
        var link = _app.UrlLoja($"/redefinir-senha?token={Uri.EscapeDataString(token)}");

        // HtmlEncode no nome: ele veio de um campo que o proprio usuario preenche, e concatenar
        // texto de entrada direto no corpo HTML de um e-mail e injecao de marcacao — da para
        // pendurar um link falso na mensagem que a loja assina.
        var saudacao = string.IsNullOrWhiteSpace(usuario.NomeCompleto)
            ? string.Empty
            : ", " + System.Net.WebUtility.HtmlEncode(usuario.NomeCompleto);

        var corpo =
            $"<p>Ola{saudacao}.</p>" +
            $"<p>Recebemos um pedido para redefinir a sua senha na {_app.NomeLoja}.</p>" +
            $"<p><a href=\"{link}\">Clique aqui para criar uma nova senha</a>.</p>" +
            $"<p>O link vale por {MinutosLinkRedefinicao} minutos e so pode ser usado uma vez.</p>" +
            "<p>Se nao foi voce, ignore esta mensagem: nada muda ate o link ser aberto.</p>";

        try
        {
            // E-mail NUNCA dentro de transacao de banco, e falha de envio nao pode virar erro
            // para o cliente: nada foi gravado, e insistir so revelaria se o endereco existe.
            await _email.EnviarAsync(
                usuario.Email,
                $"{_app.NomeLoja} - redefinicao de senha",
                corpo,
                cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao enviar o e-mail de redefinicao para o usuario {Uuid}.", usuario.Uuid);
        }
    }

    /// <inheritdoc />
    public async Task RedefinirSenhaAsync(
        RedefinirSenhaRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var uuid = _tokenRedefinicao.LerUuid(dto.Token)
            ?? throw new BusinessValidationException(LinkInvalido);

        var usuario = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.QueryTracked().Where(u => u.Uuid == uuid),
            cancellationToken);

        if (usuario is null || !usuario.Ativo)
            throw new BusinessValidationException(LinkInvalido);

        var agora = _relogio.UtcNow;

        // A assinatura inclui o hash de senha ATUAL: assim que a senha muda, este mesmo token
        // deixa de conferir. E o que garante uso unico sem tabela de tokens.
        if (!_tokenRedefinicao.Validar(dto.Token, usuario.Uuid, usuario.SenhaHash, agora))
            throw new BusinessValidationException(LinkInvalido);

        usuario.SenhaHash = Senhas.Hash(dto.NovaSenha);

        // Abrir o link prova posse da caixa de e-mail.
        usuario.EmailVerificado = true;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _refreshTokens.RevogarDoUsuarioAsync(usuario.Id, agora, cancellationToken);

        _logger.LogInformation("Senha redefinida por link. Usuario {Uuid}. Sessoes revogadas.", usuario.Uuid);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Emite o par de tokens, grava a linha de refresh e devolve a sessao pronta.
    ///
    /// Ponto unico de emissao de proposito: se cada caso de uso montasse o seu, bastaria um
    /// esquecer o IdFamilia — ou gravar o token em claro — para abrir um buraco que so aparece
    /// no incidente.
    /// </summary>
    /// <param name="substituindo">
    /// Token que esta sendo rotacionado. Recebe RevogadoEm e SubstituidoPorHash na MESMA
    /// unidade de trabalho em que o sucessor nasce: sem isso existe um instante em que os dois
    /// valem, e a deteccao de reuso passa a acusar o usuario legitimo.
    /// </param>
    private async Task<SessaoAutenticada> EmitirSessaoAsync(
        Usuario usuario,
        IReadOnlyList<string> papeis,
        Guid idFamilia,
        OrigemRequisicao origem,
        RefreshToken? substituindo,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        var acesso = _tokens.GerarAccessToken(usuario, papeis, idFamilia);
        var refresh = _tokens.GerarRefreshToken();
        var expiraRefresh = agora.AddDays(_jwt.RefreshTokenDias);

        if (substituindo is not null)
        {
            substituindo.RevogadoEm = agora;
            substituindo.SubstituidoPorHash = refresh.TokenHash;
        }

        await _refreshTokens.AdicionarAsync(
            new RefreshToken
            {
                IdUsuario = usuario.Id,
                TokenHash = refresh.TokenHash,
                CriadoEm = agora,
                ExpiraEm = expiraRefresh,
                IdFamilia = idFamilia,
                IpCriacao = Truncar(origem.Ip, 45),
                UserAgent = Truncar(origem.UserAgent, 400)
            },
            cancellationToken);

        // Quem salva e o caso de uso. O repositorio acima so registrou a intencao.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessaoAutenticada
        {
            AccessToken = acesso.Token,
            ExpiraEmSegundos = acesso.ExpiraEmSegundos,
            RefreshTokenClaro = refresh.TokenClaro,
            RefreshTokenExpiraEmUtc = expiraRefresh,
            IdSessao = idFamilia,
            Usuario = await ObterDtoAsync(usuario.Id, cancellationToken)
        };
    }

    private async Task<UsuarioResponseDto> ObterDtoAsync(int idUsuario, CancellationToken cancellationToken)
    {
        var dto = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.Query().Where(u => u.Id == idUsuario).Select(UsuarioProjecao.Resposta),
            cancellationToken);

        return dto ?? throw new EntityNotFoundException("Usuario", idUsuario);
    }

    private async Task<Usuario> ObterTrackedPorUuidAsync(string uuid, CancellationToken cancellationToken)
    {
        var usuario = await _consulta.PrimeiroOuPadraoAsync(
            _usuarios.QueryTracked().Where(u => u.Uuid == uuid),
            cancellationToken);

        // O uuid veio da claim sub de um token que a API assinou. Se nao existe usuario, o token
        // sobreviveu ao registro — tratar como sessao invalida, nunca como 404 de recurso.
        return usuario ?? throw new UnauthorizedAccessException(SessaoInvalida);
    }

    private async Task ConcederPapelAsync(int idUsuario, string papel, CancellationToken cancellationToken)
    {
        var role = await _roles.ObterPorNomeAsync(papel, cancellationToken)
            ?? throw new BusinessValidationException(
                $"Papel '{papel}' nao existe. O seed inicial de papeis nao rodou neste banco.");

        // Ids explicitos em vez da navegacao: adicionar pela colecao arrastaria a entidade Role
        // desanexada para o grafo e o EF tentaria INSERIR o papel de novo.
        await _roles.ConcederAsync(
            new UsuarioRole
            {
                IdUsuario = idUsuario,
                IdRole = role.Id,
                ConcedidaEm = _relogio.UtcNow
            },
            cancellationToken);
    }

    /// <summary>Valida o id_token e aplica as guardas de negocio que a porta nao aplica.</summary>
    private async Task<Models.Auth.GoogleIdentityInfo> ValidarGoogleAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        // Null aqui e token invalido/expirado/de outra audience — caso esperado, vira 401.
        // Falha de rede ao buscar o JWKS propaga como excecao e vira 500, que e o correto:
        // sao problemas diferentes e nao podem virar a mesma resposta.
        var identidade = await _google.ValidarAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("Login com Google invalido ou expirado. Tente novamente.");

        if (string.IsNullOrWhiteSpace(identidade.Subject))
            throw new BusinessValidationException("Token do Google sem identificador de usuario.");

        if (string.IsNullOrWhiteSpace(identidade.Email))
            throw new BusinessValidationException("Token do Google sem e-mail.");

        // Sem esta guarda, criar uma conta Google com o e-mail de outra pessoa (sem prova-lo)
        // seria suficiente para assumir a conta dela aqui pelo casamento por e-mail.
        if (!identidade.EmailVerificado)
            throw new BusinessValidationException("E-mail da conta Google nao verificado.");

        if (_googleOpcoes.DominiosPermitidos.Count > 0)
        {
            var arroba = identidade.Email.LastIndexOf('@');
            var dominio = arroba < 0 ? string.Empty : identidade.Email[(arroba + 1)..].ToLowerInvariant();

            if (!_googleOpcoes.DominiosPermitidos.Any(d => string.Equals(d?.Trim(), dominio, StringComparison.OrdinalIgnoreCase)))
                throw new BusinessValidationException("Esta conta Google nao pertence a um dominio autorizado.");
        }

        return identidade;
    }

    private static LoginExterno NovoLoginExterno(
        int idUsuario,
        Models.Auth.GoogleIdentityInfo identidade,
        string email,
        DateTime agora) =>
        new()
        {
            IdUsuario = idUsuario,
            Provedor = ProvedoresLoginExterno.Google,
            SubjectId = identidade.Subject,
            EmailNoProvedor = Truncar(email, 255)!,
            DataVinculo = agora,
            UltimoUsoEm = agora
        };

    /// <summary>Atualiza o que o Google mantem atualizado e nos nao. Nunca mexe em papel.</summary>
    private static void AplicarPerfilGoogle(Usuario usuario, Models.Auth.GoogleIdentityInfo identidade, DateTime? agora)
    {
        if (!string.IsNullOrWhiteSpace(identidade.FotoUrl))
            usuario.FotoUrl = identidade.FotoUrl;

        // Nome so e preenchido quando esta faltando: o cliente pode ter corrigido o nome aqui,
        // e sobrescrever a cada login desfaz a correcao dele em silencio.
        if (string.IsNullOrWhiteSpace(usuario.NomeCompleto) && !string.IsNullOrWhiteSpace(identidade.Nome))
            usuario.NomeCompleto = Truncar(identidade.Nome.Trim(), 180);

        if (agora is not null)
            usuario.UltimoLoginEm = agora;
    }

    private static void GarantirAtivo(Usuario usuario)
    {
        if (!usuario.Ativo)
            throw new BusinessValidationException("Esta conta esta desativada. Fale com o atendimento.");
    }

    /// <summary>Minusculas e sem espaco: o indice unico e sobre o valor gravado.</summary>
    private static string NormalizarEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Corta no limite da coluna. User-Agent de 2 KB e comum e a coluna aceita 400 — sem o
    /// corte, o login inteiro falha com erro de driver por causa de um cabecalho de auditoria.
    /// </summary>
    private static string? Truncar(string? valor, int maximo) =>
        string.IsNullOrEmpty(valor) || valor.Length <= maximo ? valor : valor[..maximo];
}
