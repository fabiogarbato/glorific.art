using Glorific.Application.DTO.Carrinho;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Carrinho SERVER-SIDE.
///
/// Duas regras estruturam este contrato:
///
/// 1. O CARRINHO NAO RESERVA ESTOQUE. Reservar no "adicionar ao carrinho" trava peca de giro
///    rapido para quem nao vai comprar. Disponibilidade aqui e informativa (badge "Esgotado",
///    aviso de quantidade acima do saldo); a autoridade e o POST /checkout.
///
/// 2. A IDENTIDADE VEM DE <see cref="IdentidadeCarrinho"/>, nunca do corpo da requisicao. O
///    uuid do usuario sai da claim sub e a chave de sessao sai do cookie. Aceitar um id de
///    carrinho enviado pelo cliente seria entregar o carrinho de qualquer pessoa a quem
///    chutasse o valor.
///
/// <see cref="ObterAsync"/> NAO cria carrinho: leitura de visitante sem carrinho devolve
/// carrinho vazio e nada e persistido. Criar linha a cada GET encheria a tabela com o trafego
/// de robo de indexacao. A criacao acontece na primeira acao real (adicionar item, aplicar
/// cupom, mesclar).
/// </summary>
public interface ICarrinhoService
{
    /// <summary>Le o carrinho. NAO cria: sem carrinho, devolve um DTO vazio.</summary>
    Task<CarrinhoResponseDto> ObterAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona ou soma quantidade. Cria o carrinho quando ainda nao existe.
    /// A quantidade final e validada contra o disponivel, sem reservar nada.
    /// </summary>
    Task<CarrinhoResponseDto> AdicionarItemAsync(
        IdentidadeCarrinho identidade,
        CarrinhoItemCreateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Define a quantidade da linha. Zero remove.</summary>
    Task<CarrinhoResponseDto> AlterarQuantidadeAsync(
        IdentidadeCarrinho identidade,
        int idItem,
        CarrinhoItemUpdateDto dto,
        CancellationToken cancellationToken = default);

    Task<CarrinhoResponseDto> RemoverItemAsync(
        IdentidadeCarrinho identidade,
        int idItem,
        CancellationToken cancellationToken = default);

    Task<CarrinhoResponseDto> EsvaziarAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default);

    /// <summary>Valida o cupom e o grava no carrinho. NAO consome uso: quem consome e o checkout.</summary>
    Task<CarrinhoResponseDto> AplicarCupomAsync(
        IdentidadeCarrinho identidade,
        CupomAplicacaoDto dto,
        CancellationToken cancellationToken = default);

    Task<CarrinhoResponseDto> RemoverCupomAsync(
        IdentidadeCarrinho identidade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Funde o carrinho anonimo no do usuario depois do login Google.
    ///
    /// Sem carrinho do usuario, o anonimo e ADOTADO (troca de dono, zero copia). Com os dois,
    /// as quantidades sao SOMADAS respeitando o disponivel, e o anonimo sai do estado Aberto
    /// para liberar o slot do indice unico parcial por sessao.
    /// </summary>
    Task<CarrinhoResponseDto> MesclarAsync(
        string uuidUsuario,
        string? chaveSessaoAnonima,
        CancellationToken cancellationToken = default);
}
