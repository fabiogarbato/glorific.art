using Glorific.Application.DTO.Clientes;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Clientes;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Lista de desejos.
///
/// A unicidade e por (usuario, produto), e nao por (usuario, produto, variacao), de proposito:
/// em moda o cliente favorita a PECA e depois decide o tamanho. Permitir a mesma peca tres vezes
/// com variacoes diferentes encheria a lista de duplicatas visuais identicas.
///
/// Toda operacao recebe o idUsuario e o leva PARA DENTRO da consulta. Buscar a linha so pelo id e
/// depois comparar o dono em memoria vazaria a existencia do item alheio pela diferenca entre 403
/// e 404 — aqui o resultado e sempre 404.
/// </summary>
public class ListaDesejoService : IListaDesejoService
{
    private readonly IListaDesejoRepository _listaDesejos;
    private readonly IProdutoRepository _produtos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IConsultaAssincrona _consulta;
    private readonly IClock _relogio;

    public ListaDesejoService(
        IListaDesejoRepository listaDesejos,
        IProdutoRepository produtos,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
    {
        _listaDesejos = listaDesejos ?? throw new ArgumentNullException(nameof(listaDesejos));
        _produtos = produtos ?? throw new ArgumentNullException(nameof(produtos));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ListaDesejoItemResponseDto>> ListarAsync(
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        var itens = await _listaDesejos.ObterDoUsuarioAsync(idUsuario, cancellationToken);

        return [.. itens.Select(item => _mapper.Map<ListaDesejoItemResponseDto>(item))];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<int>> ObterIdsProdutoAsync(
        int idUsuario,
        CancellationToken cancellationToken = default) =>
        _listaDesejos.ObterIdsProdutoDoUsuarioAsync(idUsuario, cancellationToken);

    /// <inheritdoc />
    public async Task<ListaDesejoItemResponseDto> AdicionarAsync(
        int idUsuario,
        ListaDesejoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existente = await _listaDesejos.ObterItemAsync(idUsuario, dto.IdProduto, cancellationToken);

        if (existente is not null)
        {
            // Idempotente. Clicar duas vezes no coracao nao pode virar erro na cara do cliente,
            // e a segunda chamada ainda serve para trocar a variacao escolhida.
            if (dto.IdVariacao is not null && existente.IdVariacao != dto.IdVariacao)
            {
                await GarantirVariacaoDoProdutoAsync(dto.IdProduto, dto.IdVariacao.Value, cancellationToken);

                existente.IdVariacao = dto.IdVariacao;
                _listaDesejos.Atualizar(existente);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return await ProjetarAsync(idUsuario, dto.IdProduto, cancellationToken);
        }

        if (!await _produtos.ExisteAsync(dto.IdProduto, cancellationToken))
            throw new EntityNotFoundException("Produto", dto.IdProduto);

        if (dto.IdVariacao is { } idVariacao)
            await GarantirVariacaoDoProdutoAsync(dto.IdProduto, idVariacao, cancellationToken);

        var item = new ListaDesejoItem
        {
            IdUsuario = idUsuario,
            IdProduto = dto.IdProduto,
            IdVariacao = dto.IdVariacao,

            // ListaDesejoItem nao e IAuditable: o carimbo nao vem do DbContext e a ordenacao da
            // lista depende dele. IClock, nunca DateTime.UtcNow direto.
            DataCriacao = _relogio.UtcNow
        };

        await _listaDesejos.AdicionarAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(idUsuario, dto.IdProduto, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoverAsync(int idUsuario, int idProduto, CancellationToken cancellationToken = default)
    {
        var item = await _listaDesejos.ObterItemAsync(idUsuario, idProduto, cancellationToken)
            ?? throw new EntityNotFoundException("Item da lista de desejos", idProduto);

        _listaDesejos.Remover(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AlternarAsync(
        int idUsuario,
        ListaDesejoCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existente = await _listaDesejos.ObterItemAsync(idUsuario, dto.IdProduto, cancellationToken);

        if (existente is null)
        {
            await AdicionarAsync(idUsuario, dto, cancellationToken);
            return true;
        }

        _listaDesejos.Remover(existente);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return false;
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>
    /// Recarrega o item com o grafo de vitrine (capa, tamanho, cor, estoque) para a resposta sair
    /// igual a listagem. Devolver o item cru depois de gravar deixaria o card sem foto e sem preco
    /// ate o proximo refresh.
    /// </summary>
    private async Task<ListaDesejoItemResponseDto> ProjetarAsync(
        int idUsuario,
        int idProduto,
        CancellationToken cancellationToken)
    {
        var itens = await _listaDesejos.ObterDoUsuarioAsync(idUsuario, cancellationToken);

        var item = itens.FirstOrDefault(linha => linha.IdProduto == idProduto)
            ?? throw new EntityNotFoundException("Item da lista de desejos", idProduto);

        return _mapper.Map<ListaDesejoItemResponseDto>(item);
    }

    /// <summary>
    /// A variacao tem de ser DAQUELE produto. Sem esta checagem, um id de variacao trocado no
    /// corpo da requisicao geraria aviso de "voltou ao estoque" do tamanho de outra peca.
    /// </summary>
    private async Task GarantirVariacaoDoProdutoAsync(
        int idProduto,
        int idVariacao,
        CancellationToken cancellationToken)
    {
        var pertence = await _consulta.AlgumAsync(
            _produtos.Query()
                .Where(produto => produto.Id == idProduto
                                  && produto.Variacoes.Any(variacao => variacao.Id == idVariacao)),
            cancellationToken);

        if (!pertence)
            throw new BusinessValidationException("O tamanho escolhido nao pertence a este produto.");
    }
}
