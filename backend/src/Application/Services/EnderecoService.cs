using Glorific.Application.DTO.Conta;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Enderecos de entrega do cliente.
///
/// Duas coisas moram aqui e em nenhum outro lugar:
/// 1. OWNERSHIP. O id do dono entra no WHERE, nunca num if depois de carregar. Quando o
///    endereco existe mas e de outro cliente a resposta e 404 — 403 confirmaria que aquele id
///    existe, e uma varredura de ids passaria a mapear quantos enderecos a loja tem.
/// 2. NORMALIZACAO. CEP e telefone perdem a mascara, UF vai para maiuscula. A coluna cep tem 8
///    caracteres: deixar "12.345-678" chegar ao banco troca um 400 com o campo culpado por um
///    500 de driver.
///
/// Consulta de CEP nao entra aqui: o front chama o ViaCEP e manda o endereco preenchido.
/// </summary>
public sealed class EnderecoService : IEnderecoService
{
    private readonly IEnderecoRepository _enderecos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IConsultaAssincrona _consulta;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EnderecoService(
        IEnderecoRepository enderecos,
        IUsuarioRepository usuarios,
        IConsultaAssincrona consulta,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _enderecos = enderecos;
        _usuarios = usuarios;
        _consulta = consulta;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnderecoResponseDto>> ListarAsync(
        string uuidUsuario,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);

        var enderecos = await _enderecos.ObterDoUsuarioAsync(usuario.Id, cancellationToken);

        return [.. enderecos.Select(endereco => _mapper.Map<EnderecoResponseDto>(endereco))];
    }

    /// <inheritdoc />
    public async Task<EnderecoResponseDto> ObterAsync(
        string uuidUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);

        var endereco = await _enderecos.ObterDoUsuarioAsync(usuario.Id, idEndereco, cancellationToken)
            ?? throw new EntityNotFoundException("Endereco", idEndereco);

        return _mapper.Map<EnderecoResponseDto>(endereco);
    }

    /// <inheritdoc />
    public async Task<EnderecoResponseDto> CriarAsync(
        string uuidUsuario,
        EnderecoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);

        var endereco = new Endereco
        {
            IdUsuario = usuario.Id,
            Destinatario = string.Empty,
            TelefoneContato = string.Empty,
            Cep = string.Empty,
            Logradouro = string.Empty,
            Numero = string.Empty,
            Bairro = string.Empty,
            Cidade = string.Empty,
            Uf = string.Empty,
            Pais = "BR",
            Ativo = true
        };

        AplicarCampos(
            endereco,
            dto.Apelido, dto.Destinatario, dto.DocumentoDestinatario, dto.TelefoneContato,
            dto.Cep, dto.Logradouro, dto.Numero, dto.Complemento, dto.Bairro, dto.Cidade, dto.Uf);

        var jaTemAlgum = await _consulta.AlgumAsync(
            _enderecos.Query().Where(e => e.IdUsuario == usuario.Id && e.Ativo),
            cancellationToken);

        // O primeiro endereco vira principal sozinho: sem isso o cliente cadastra um endereco,
        // vai ao checkout e nao ha entrega pre-selecionada nenhuma.
        var deveSerPrincipal = dto.Principal || !jaTemAlgum;

        if (deveSerPrincipal)
            await _enderecos.DesmarcarPrincipaisAsync(usuario.Id, cancellationToken);

        endereco.Principal = deveSerPrincipal;

        await _enderecos.AdicionarAsync(endereco, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<EnderecoResponseDto>(endereco);
    }

    /// <inheritdoc />
    public async Task<EnderecoResponseDto> AtualizarAsync(
        string uuidUsuario,
        int idEndereco,
        EnderecoUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);
        var endereco = await ObterParaEdicaoAsync(usuario.Id, idEndereco, cancellationToken);

        AplicarCampos(
            endereco,
            dto.Apelido, dto.Destinatario, dto.DocumentoDestinatario, dto.TelefoneContato,
            dto.Cep, dto.Logradouro, dto.Numero, dto.Complemento, dto.Bairro, dto.Cidade, dto.Uf);

        // Sem Repositorio.Atualizar: a entidade veio rastreada e o ChangeTracker ja sabe o que
        // mudou. Chamar Update aqui marcaria TODAS as colunas como alteradas e transformaria a
        // troca de um complemento num UPDATE de linha inteira.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<EnderecoResponseDto>(endereco);
    }

    /// <inheritdoc />
    public async Task RemoverAsync(
        string uuidUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);
        var endereco = await ObterParaEdicaoAsync(usuario.Id, idEndereco, cancellationToken);

        // Soft delete: pedidos antigos guardam o proprio snapshot, mas DELETE de verdade
        // quebraria qualquer relatorio que ainda referencie a linha.
        endereco.Ativo = false;

        // Endereco removido nao pode continuar sendo o principal, senao o checkout seleciona
        // por padrao um endereco que o cliente nem enxerga mais na lista.
        endereco.Principal = false;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EnderecoResponseDto> DefinirPrincipalAsync(
        string uuidUsuario,
        int idEndereco,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(uuidUsuario, cancellationToken);

        // Confere o dono ANTES de mexer em qualquer linha: sem esta leitura, um id de outra
        // pessoa desmarcaria o principal do usuario atual e depois falharia com 404, deixando a
        // conta sem endereco principal nenhum.
        var alvo = await _enderecos.ObterDoUsuarioAsync(usuario.Id, idEndereco, cancellationToken)
            ?? throw new EntityNotFoundException("Endereco", idEndereco);

        if (!alvo.Ativo)
            throw new BusinessValidationException("Este endereco foi removido e nao pode ser o principal.");

        // UPDATE em bloco primeiro: dois principais ao mesmo tempo e um estado que o checkout
        // resolve escolhendo o primeiro que vier, ou seja, aleatoriamente.
        await _enderecos.DesmarcarPrincipaisAsync(usuario.Id, cancellationToken);

        // Carregado DEPOIS do UPDATE em bloco de proposito: o ExecuteUpdate desanexa do
        // rastreamento o que ele tocou, e uma instancia carregada antes sairia do SaveChanges
        // sem efeito nenhum.
        var endereco = await ObterParaEdicaoAsync(usuario.Id, idEndereco, cancellationToken);

        endereco.Principal = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<EnderecoResponseDto>(endereco);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    private async Task<Usuario> ObterUsuarioAsync(string uuidUsuario, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ObterPorUuidAsync(uuidUsuario, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sessao expirada. Entre novamente.");

        if (!usuario.Ativo)
            throw new BusinessValidationException("Esta conta esta desativada. Fale com o atendimento.");

        return usuario;
    }

    /// <summary>Rastreado E filtrado pelo dono na mesma consulta. Nao existe versao sem o dono.</summary>
    private async Task<Endereco> ObterParaEdicaoAsync(
        int idUsuario,
        int idEndereco,
        CancellationToken cancellationToken)
    {
        var endereco = await _consulta.PrimeiroOuPadraoAsync(
            _enderecos.QueryTracked().Where(e => e.Id == idEndereco && e.IdUsuario == idUsuario),
            cancellationToken);

        // 404 e nao 403: confirmar a existencia do id ja seria informacao a mais.
        return endereco ?? throw new EntityNotFoundException("Endereco", idEndereco);
    }

    private static void AplicarCampos(
        Endereco endereco,
        string? apelido,
        string destinatario,
        string? documento,
        string telefone,
        string cep,
        string logradouro,
        string numero,
        string? complemento,
        string bairro,
        string cidade,
        string uf)
    {
        var cepDigitos = CepHelper.SomenteDigitos(cep);

        if (!CepHelper.Valido(cepDigitos))
            throw new BusinessValidationException("CEP invalido.");

        var telefoneDigitos = TelefoneHelper.SomenteDigitos(telefone);

        if (!TelefoneHelper.Valido(telefoneDigitos))
            throw new BusinessValidationException("Telefone de contato invalido. Informe DDD e numero.");

        var documentoDigitos = DocumentoHelper.SomenteDigitos(documento);

        if (documentoDigitos.Length > 0
            && !DocumentoHelper.CpfValido(documentoDigitos)
            && !DocumentoHelper.CnpjValido(documentoDigitos))
        {
            // O documento do destinatario e exigido pela transportadora na compra da etiqueta.
            // Recusar aqui evita o pior caminho: a etiqueta falhar DEPOIS de o cliente pagar.
            throw new BusinessValidationException("Documento do destinatario invalido. Informe um CPF ou CNPJ.");
        }

        var ufNormalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();

        if (ufNormalizada.Length != 2 || !ufNormalizada.All(char.IsLetter))
            throw new BusinessValidationException("UF invalida. Informe a sigla de dois caracteres.");

        endereco.Apelido = Vazio(apelido);
        endereco.Destinatario = destinatario.Trim();
        endereco.DocumentoDestinatario = documentoDigitos.Length == 0 ? null : documentoDigitos;
        endereco.TelefoneContato = telefoneDigitos;
        endereco.Cep = cepDigitos;
        endereco.Logradouro = logradouro.Trim();
        endereco.Numero = numero.Trim();
        endereco.Complemento = Vazio(complemento);
        endereco.Bairro = bairro.Trim();
        endereco.Cidade = cidade.Trim();
        endereco.Uf = ufNormalizada;
        endereco.Pais = "BR";
    }

    /// <summary>String em branco vira null: coluna opcional com "" e ruido que ninguem filtra.</summary>
    private static string? Vazio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
